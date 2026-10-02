using AlFolk.Api.Data;
using AlFolk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFolk.Api.Services;

public record SearchResultItem(Guid Id, string Name, string? Detail, string Type);

public record SearchResponse(
    List<SearchResultItem> Children,
    List<SearchResultItem> Parents
);

public interface ISearchService
{
    Task<SearchResponse> GlobalSearchAsync(string q);
}

public class SearchService : ISearchService
{
    private readonly AppDbContext _context;

    public SearchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SearchResponse> GlobalSearchAsync(string q)
    {
        var normalizedQuery = ArabicNormalization.Normalize(q);
        if (normalizedQuery.Length < 2) return new SearchResponse(new(), new());

        var children = await _context.Children.Include(c => c.Parent).ToListAsync();
        var parents = await _context.Users.Where(u => u.Role == "Parent").ToListAsync();

        var childrenResults = children
            .Where(c => ArabicNormalization.Normalize(c.Name).Contains(normalizedQuery))
            .Take(8)
            .Select(c => new SearchResultItem(c.Id, c.Name, c.Parent.FirstName + " " + c.Parent.LastName, "child"))
            .ToList();

        var parentsResults = parents
            .Where(u => ArabicNormalization.Normalize(u.FirstName + " " + u.LastName).Contains(normalizedQuery) || (u.Phone != null && u.Phone.Contains(q)))
            .Take(8)
            .Select(u => new SearchResultItem(u.Id, u.FirstName + " " + u.LastName, u.Phone, "parent"))
            .ToList();

        return new SearchResponse(childrenResults, parentsResults);
    }
}
