using System;

namespace CrsterCommand.Models;

public enum BudgetFrequency
{
    Daily,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum BudgetTransactionType
{
    Income,
    Expense,
    Reserve
}

public class BudgetTransaction
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public BudgetTransactionType Type { get; set; }
    public bool IsRecurring { get; set; }
    public BudgetFrequency? Frequency { get; set; }
    public DateTime DueDate { get; set; } = DateTime.Today;
}

public class BudgetSnapshot
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public decimal BalanceAtSnapshot { get; set; }
    public int LastProcessedTransactionId { get; set; }
}
