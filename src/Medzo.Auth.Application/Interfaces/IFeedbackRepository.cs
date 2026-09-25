using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Interfaces;

public interface IFeedbackRepository
{
    Task<IReadOnlyList<Review>> GetReviewsAsync();
    Task<Review> AddReviewAsync(Review review);
    Task<ContactMessage> AddContactMessageAsync(ContactMessage message);
}

