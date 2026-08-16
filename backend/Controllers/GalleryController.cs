using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Controllers;

[ApiController]
[Route("api/gallery")]
public class GalleryController(AppDbContext db, FileStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GalleryItem>>> GetAll()
    {
        var items = await db.GalleryItems.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return Ok(items);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<GalleryItem>> Create(
        [FromForm] string title,
        [FromForm] string description,
        [FromForm] string category,
        [FromForm] string material,
        [FromForm] IFormFile image)
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
        return Created($"/api/gallery/{item.Id}", item);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<GalleryItem>> Update(
        int id,
        [FromForm] string title,
        [FromForm] string description,
        [FromForm] string category,
        [FromForm] string material,
        [FromForm] IFormFile? image)
    {
        var item = await db.GalleryItems.FindAsync(id);
        if (item is null) return NotFound();

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
        return Ok(item);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.GalleryItems.FindAsync(id);
        if (item is null) return NotFound();

        storage.Delete(item.ImageUrl);
        db.GalleryItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
