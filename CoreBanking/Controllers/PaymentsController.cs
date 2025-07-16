using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using CoreBanking;

namespace CoreBanking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly BankingDbContext _db;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(BankingDbContext db, ILogger<PaymentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("ach")]
    public async Task<IActionResult> InitiateAch([FromBody] Transaction tx)
    {
        tx.Type = TransactionType.ACH;
        tx.Status = TransactionStatus.Pending;
        tx.CreatedAt = DateTime.UtcNow;
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        // Audit log
        _db.TransactionAuditLogs.Add(new TransactionAuditLog
        {
            TransactionId = tx.Id,
            Action = "ACH Initiated",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"ACH payment initiated: {tx.Amount} {tx.Currency}"
        });
        await _db.SaveChangesAsync();
        // TODO: Publish event to RabbitMQ for async processing
        return Accepted(new { tx.Id, tx.Status });
    }

    [HttpPost("wire")]
    public async Task<IActionResult> InitiateWire([FromBody] Transaction tx)
    {
        tx.Type = TransactionType.Wire;
        tx.Status = TransactionStatus.Pending;
        tx.CreatedAt = DateTime.UtcNow;
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        // Audit log
        _db.TransactionAuditLogs.Add(new TransactionAuditLog
        {
            TransactionId = tx.Id,
            Action = "Wire Initiated",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Wire payment initiated: {tx.Amount} {tx.Currency}"
        });
        await _db.SaveChangesAsync();
        // TODO: Publish event to RabbitMQ for async processing
        return Accepted(new { tx.Id, tx.Status });
    }

    [HttpPost("card")]
    public async Task<IActionResult> IssueCard([FromBody] Account account)
    {
        // Placeholder for card issuance logic
        // TODO: Integrate with card network and HSM
        return Accepted(new { Message = "Card issuance initiated (placeholder)" });
    }
} 