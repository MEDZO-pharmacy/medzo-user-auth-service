using Medzo.Auth.Application.DTOs;

namespace Medzo.Auth.Application.Interfaces;

public interface IFeedbackService
{
    Task<IReadOnlyList<ReviewResponse>> GetReviewsAsync();
    Task<ReviewResponse> AddReviewAsync(ReviewRequest request);
    Task<Guid> AddContactMessageAsync(ContactMessageRequest request);
}

