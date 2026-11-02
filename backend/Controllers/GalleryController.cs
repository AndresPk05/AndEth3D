using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api/gallery")]
public class GalleryController(GalleryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => (await service.GetAll()).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpPost, RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Create(
        [FromForm, Required, StringLength(200)] string title,
        [FromForm, Required, StringLength(5000)] string description,
        [FromForm, Required, StringLength(100)] string category,
        [FromForm, Required, StringLength(100)] string material,
        [FromForm] IFormFile image) => (await service.Create(title, description, category, material, image)).ToHttp(this, "/api/gallery");
    [Authorize(Roles = Roles.Admin), HttpPut("{id:int}"), RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Update(int id,
        [FromForm, Required, StringLength(200)] string title,
        [FromForm, Required, StringLength(5000)] string description,
        [FromForm, Required, StringLength(100)] string category,
        [FromForm, Required, StringLength(100)] string material,
        [FromForm] IFormFile? image) => (await service.Update(id, title, description, category, material, image)).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) => (await service.Delete(id)).ToHttp(this);
}