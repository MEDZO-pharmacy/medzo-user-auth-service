using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Domain.Entities;

namespace Medzo.Auth.Application.Services;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _repository;

    public FeedbackService(IFeedbackRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ReviewResponse>> GetReviewsAsync() =>
        (await _repository.GetReviewsAsync()).Select(Map).ToArray();

    public async Task<ReviewResponse> AddReviewAsync(ReviewRequest request)
    {
        var review = await _repository.AddReviewAsync(new Review
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CustomerType = request.CustomerType,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        return Map(review);
    }

    public async Task<Guid> AddContactMessageAsync(ContactMessageRequest request)
    {
        var message = await _repository.AddContactMessageAsync(new ContactMessage
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        return message.Id;
    }

    private static ReviewResponse Map(Review review) => new()
    {
        Id = review.Id,
        Name = review.Name,
        CustomerType = review.CustomerType,
        Rating = review.Rating,
        Comment = review.Comment,
        CreatedAt = review.CreatedAt
    };
}
