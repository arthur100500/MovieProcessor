using DatabaseInteraction;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Web.Components.Misc;

public class Pagination<T> where T : class, IWithNumericalId
{
    public IQueryable<T> Collection;
    public const int PageSize = 20;

    public Pagination(IQueryable<T> collection)
    {
        Collection = collection;
    }

    public async Task<ICollection<T>> SelectPage(int page, bool useNid)
    {
        var maxPage = GetMaxPage();

        if (page >= maxPage || page < 0)
            throw new ArgumentOutOfRangeException($"Page out of range [0, {maxPage})");

        if (useNid)
        {
            var selectedRange = await Collection
                .Where(x => x.NumericalId >= page * PageSize && x.NumericalId < (page + 1) * PageSize)
                .ToListAsync();

            return selectedRange;
        }

        var allRange = await Collection
            .ToListAsync();

        return allRange.Slice(PageSize * page, Math.Min(PageSize * (page + 1), allRange.Count) - PageSize * page);
    }

    public int GetMaxPage()
    {
        var entryCount = Collection.Count();
        var maxPage = (int)MathF.Ceiling((float)entryCount / PageSize);
        return maxPage;
    }
}