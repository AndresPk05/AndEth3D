using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Services;
public class GalleryService(AppDbContext db, FileStorage storage)
{
    public async Task<AppResult> GetAll()
    {
        var items = await db.GalleryItems.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return AppResult.Ok(items);
    }
    public async Task<AppResult> Create(
        string title,
        string description,
        string category,
        string material,
        IFormFile image)
    {
        var item = new GalleryItem
        {
            Title = title,
            Description = description,
            Category = category,
            Material = material,
            ImageUrl = await storage.SaveAsync(image),
        };

        db.GalleryItems.Add(item);
        await db.SaveChangesAsync();
        return AppResult.Created(item.Id.ToString(), item);
    }
    public async Task<AppResult> Update(
        int id,
        string title,
        string description,
        string category,
        string material,
        IFormFile? image)
    {
        var item = await db.GalleryItems.FindAsync(id);
        if (item is null) return AppResult.NotFound();

        item.Title = title;
        item.Description = description;
        item.Category = category;
        item.Material = material;

        if (image is not null)
        {
            var oldUrl = item.ImageUrl;
            item.ImageUrl = await storage.SaveAsync(image);
            storage.Delete(oldUrl);
        }

        await db.SaveChangesAsync();
        return AppResult.Ok(item);
    }
    public async Task<AppResult> Delete(int id)
    {
        var item = await db.GalleryItems.FindAsync(id);
        if (item is null) return AppResult.NotFound();

        storage.Delete(item.ImageUrl);
        db.GalleryItems.Remove(item);
        await db.SaveChangesAsync();
        return AppResult.NoContent();
    }
}
