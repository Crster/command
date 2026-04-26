using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrsterCommand.Models;
using CrsterCommand.Services;

namespace CrsterCommand.ViewModels;

public partial class BudgetViewModel : ViewModelBase
{
    private readonly StorageService _storageService;

    // ── Observable state ───────────────────────────────────────────────
    public ObservableCollection<BudgetTransaction> Transactions { get; } = new();
    public ObservableCollection<BudgetTransaction> MonthTransactions { get; } = new();

    [ObservableProperty] private decimal _currentBalance;
    [ObservableProperty] private decimal _upcomingDueAmount;
    [ObservableProperty] private decimal _upcomingIncome;
    [ObservableProperty] private decimal _targetSavings;
    [ObservableProperty] private decimal _spendableThisWeek;
    [ObservableProperty] private string _lastSnapshotLabel = "No snapshot yet";
    [ObservableProperty] private bool _isAddFormExpanded;
    [ObservableProperty] private DateTime _historyMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);

    public string HistoryMonthLabel => HistoryMonth.ToString("MMMM yyyy");

    // ── Add-transaction form fields ─────────────────────────────────────
    [ObservableProperty] private string _newDescription = "";
    [ObservableProperty] private string _newAmountText = "";
    [ObservableProperty] private BudgetTransactionType _newType = BudgetTransactionType.Income;
    [ObservableProperty] private bool _newIsRecurring;
    [ObservableProperty] private BudgetFrequency _newFrequency = BudgetFrequency.Monthly;
    // Use DateTime? to match CalendarDatePicker.SelectedDate (nullable DateTime)
    [ObservableProperty] private DateTime? _newDueDate = DateTime.Now;

    public BudgetTransactionType[] TransactionTypes { get; } =
        Enum.GetValues<BudgetTransactionType>();

    public BudgetFrequency[] Frequencies { get; } =
        Enum.GetValues<BudgetFrequency>();

    public BudgetViewModel(StorageService storageService)
    {
        _storageService = storageService;
        EnsureIndexes();
        Refresh();
    }

    // ── Indexes ─────────────────────────────────────────────────────────
    private void EnsureIndexes()
    {
        var txCol = _storageService.GetBudgetTransactions();
        txCol.EnsureIndex(t => t.DueDate);
        txCol.EnsureIndex(t => t.Id);

        var snapCol = _storageService.GetBudgetSnapshots();
        snapCol.EnsureIndex(s => s.Timestamp);
    }

    // ── Refresh / load ───────────────────────────────────────────────────
    private void Refresh()
    {
        var txCol = _storageService.GetBudgetTransactions();
        var snapCol = _storageService.GetBudgetSnapshots();

        // Latest snapshot for delta base
        var latestSnap = snapCol.FindAll().OrderByDescending(s => s.Timestamp).FirstOrDefault();

        // All transactions after the snapshot's last-processed id
        var allTx = txCol.FindAll().OrderBy(t => t.Id).ToList();
        var baseTxId = latestSnap?.LastProcessedTransactionId ?? 0;
        var baseBalance = latestSnap?.BalanceAtSnapshot ?? 0m;

        var balanceStart = latestSnap?.Timestamp.Date.AddDays(1) ?? DateTime.MinValue;
        var balanceEnd = DateTime.Now.Date;

        var deltaBalance = allTx
            .Where(t => t.Id > baseTxId)
            .Sum(t => GetSignedEffectiveAmount(t, balanceStart, balanceEnd));

        CurrentBalance = baseBalance + deltaBalance;

        // Snapshot label
        LastSnapshotLabel = latestSnap != null
            ? $"Last snapshot: {latestSnap.Timestamp:MMM d, yyyy h:mm tt}"
            : "No snapshot yet";

        // History ledger month
        var now = DateTime.Now;
        var monthStart = new DateTime(HistoryMonth.Year, HistoryMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        MonthTransactions.Clear();
        foreach (var t in allTx.Where(t => t.DueDate >= monthStart && t.DueDate <= monthEnd)
                               .OrderByDescending(t => t.DueDate))
            MonthTransactions.Add(t);

        // Upcoming window: tomorrow → end of next month
        var upcomingStart = now.Date.AddDays(1);
        var upcomingEnd = new DateTime(now.Year, now.Month, 1).AddMonths(2).AddTicks(-1);
        var spendableEnd = now.Date.AddDays(7);

        decimal upcomingIncome = 0m;
        decimal upcomingExpenses = 0m;
        decimal spendableExpenses = 0m;
        decimal reserveFixed = 0m;
        decimal dueSoon = 0m;

        foreach (var t in allTx)
        {
            var upcomingEffective = EffectiveAmountInWindow(t, upcomingStart, upcomingEnd);
            var spendableEffective = EffectiveAmountInWindow(t, upcomingStart, spendableEnd);

            switch (t.Type)
            {
                case BudgetTransactionType.Income:
                    upcomingIncome += upcomingEffective;
                    break;
                case BudgetTransactionType.Expense:
                    upcomingExpenses += upcomingEffective;
                    spendableExpenses += spendableEffective;
                    dueSoon += upcomingEffective;
                    break;
                case BudgetTransactionType.Reserve:
                    reserveFixed += t.Amount; // fixed amount, not percentage
                    break;
            }
        }

        UpcomingDueAmount = dueSoon;
        UpcomingIncome = upcomingIncome;
        TargetSavings = reserveFixed;
        // Spendable uses the actual balance derived from snapshots plus transactions up to today,
        // with an 80% safety buffer.
        var spendable = CurrentBalance - (spendableExpenses + reserveFixed);
        SpendableThisWeek = Math.Max(0m, spendable * 0.8m);
    }

    partial void OnHistoryMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(HistoryMonthLabel));
        Refresh();
    }

    /// <summary>
    /// Calculates the effective amount of a transaction that falls within the given window,
    /// expanding recurring transactions to their occurrences in the window.
    /// </summary>
    private static decimal EffectiveAmountInWindow(BudgetTransaction t, DateTime start, DateTime end)
    {
        if (!t.IsRecurring || t.Frequency == null)
        {
            return t.DueDate >= start && t.DueDate <= end ? t.Amount : 0m;
        }

        var span = t.Frequency.Value switch
        {
            BudgetFrequency.Daily => TimeSpan.FromDays(1),
            BudgetFrequency.Weekly => TimeSpan.FromDays(7),
            BudgetFrequency.BiWeekly => TimeSpan.FromDays(14),
            BudgetFrequency.Monthly => TimeSpan.FromDays(30),
            BudgetFrequency.Quarterly => TimeSpan.FromDays(91),
            BudgetFrequency.Yearly => TimeSpan.FromDays(365),
            _ => TimeSpan.FromDays(30)
        };

        decimal total = 0m;
        var occ = t.DueDate;
        while (occ <= end)
        {
            if (occ >= start) total += t.Amount;
            occ = occ.Add(span);
        }
        return total;
    }

    private static decimal GetSignedEffectiveAmount(BudgetTransaction t, DateTime start, DateTime end)
    {
        var effectiveAmount = EffectiveAmountInWindow(t, start, end);

        return t.Type switch
        {
            BudgetTransactionType.Income => effectiveAmount,
            BudgetTransactionType.Expense => -effectiveAmount,
            BudgetTransactionType.Reserve => 0m,
            _ => 0m
        };
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleAddForm()
    {
        IsAddFormExpanded = !IsAddFormExpanded;
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        HistoryMonth = HistoryMonth.AddMonths(-1);
    }

    [RelayCommand]
    private void NextMonth()
    {
        HistoryMonth = HistoryMonth.AddMonths(1);
    }

    [RelayCommand]
    private void AddTransaction()
    {
        if (string.IsNullOrWhiteSpace(NewDescription))
            return;

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
            return;

        var tx = new BudgetTransaction
        {
            Description = NewDescription.Trim(),
            Amount = amount,
            Type = NewType,
            IsRecurring = NewIsRecurring,
            Frequency = NewIsRecurring ? NewFrequency : null,
            DueDate = NewDueDate?.Date ?? DateTime.Today
        };

        _storageService.GetBudgetTransactions().Insert(tx);

        // Reset form
        NewDescription = "";
        NewAmountText = "";
        NewIsRecurring = false;

        Refresh();
    }

    [RelayCommand]
    private void DeleteTransaction(BudgetTransaction transaction)
    {
        _storageService.GetBudgetTransactions().Delete(transaction.Id);
        Refresh();
    }

    [RelayCommand]
    private void Recalculate()
    {
        var txCol = _storageService.GetBudgetTransactions();
        var snapCol = _storageService.GetBudgetSnapshots();

        var latestSnap = snapCol.FindAll().OrderByDescending(s => s.Timestamp).FirstOrDefault();
        var baseTxId = latestSnap?.LastProcessedTransactionId ?? 0;
        var baseBalance = latestSnap?.BalanceAtSnapshot ?? 0m;

        var now = DateTime.Now;

        // Only process transactions after the last snapshot (delta only)
        var delta = txCol.Find(t => t.Id > baseTxId)
                         .OrderBy(t => t.Id)
                         .ToList();

        if (!delta.Any())
        {
            // Nothing new; write an identical snapshot to record the audit
            var noOpSnap = new BudgetSnapshot
            {
                Timestamp = DateTime.Now,
                BalanceAtSnapshot = baseBalance,
                LastProcessedTransactionId = baseTxId
            };
            snapCol.Insert(noOpSnap);
            LastSnapshotLabel = $"Last snapshot: {noOpSnap.Timestamp:MMM d, yyyy h:mm tt}";
            return;
        }

        // Recalculate current balance based only on transactions effective up to now.
        var recalculationStart = latestSnap?.Timestamp.Date.AddDays(1) ?? DateTime.MinValue;
        var newBalance = baseBalance + delta.Sum(t => GetSignedEffectiveAmount(t, recalculationStart, now.Date));

        var snap = new BudgetSnapshot
        {
            Timestamp = now,
            BalanceAtSnapshot = newBalance,
            LastProcessedTransactionId = delta.Last().Id
        };

        snapCol.Insert(snap);
        Refresh();
    }
}
