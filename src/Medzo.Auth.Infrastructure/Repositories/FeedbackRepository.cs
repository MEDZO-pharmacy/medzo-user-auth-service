using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Medzo.Auth.Infrastructure.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly AuthDbContext _context;

    public FeedbackRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Review>> GetReviewsAsync() =>
        await _context.Reviews.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();

    public async Task<Review> AddReviewAsync(Review review)
    {
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();
        return review;
    }

    public async Task<ContactMessage> AddContactMessageAsync(ContactMessage message)
    {
        _context.ContactMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }
}

