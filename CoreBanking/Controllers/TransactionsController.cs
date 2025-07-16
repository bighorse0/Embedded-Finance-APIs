using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using CoreBanking;

namespace CoreBanking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly BankingDbContext _db;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(BankingDbContext db, ILogger<TransactionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
    {
        return await _db.Transactions.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Transaction>> GetTransaction(Guid id)
    {
        var tx = await _db.Transactions.FindAsync(id);
        if (tx == null) return NotFound();
        return tx;
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> CreateTransaction(Transaction tx)
    {
        tx.Id = Guid.NewGuid();
        tx.CreatedAt = DateTime.UtcNow;
        tx.Status = TransactionStatus.Pending;
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        // Audit log
        _db.TransactionAuditLogs.Add(new TransactionAuditLog
        {
            TransactionId = tx.Id,
            Action = "Created",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Transaction created: {tx.Type} {tx.Amount} {tx.Currency}"
        });
        await _db.SaveChangesAsync();
        // TODO: Publish event to RabbitMQ for async processing
        return CreatedAtAction(nameof(GetTransaction), new { id = tx.Id }, tx);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TransactionStatus status)
    {
        var tx = await _db.Transactions.FindAsync(id);
        if (tx == null) return NotFound();
        tx.Status = status;
        tx.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        // Audit log
        _db.TransactionAuditLogs.Add(new TransactionAuditLog
        {
            TransactionId = tx.Id,
            Action = "StatusUpdated",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Status changed to {status}"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }
} 