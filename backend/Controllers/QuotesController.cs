using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api/quotes")]
public class QuotesController(QuotesService service) : ControllerBase
{
    [Authorize, HttpPost, RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Create(
        [FromForm, Required, StringLength(5000)] string description,
        [FromForm, Required, StringLength(100)] string material,
        [FromForm] IFormFile? referenceFile) => (await service.Create(description, material, referenceFile)).ToHttp(this, "/api/quotes");
    [Authorize, HttpGet("mine")]
    public async Task<IActionResult> Mine() => (await service.Mine()).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpGet]
    public async Task<IActionResult> GetAll() => (await service.GetAll()).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateQuoteRequest request) => (await service.Update(id, request)).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, ConvertQuoteRequest request) => (await service.Convert(id, request)).ToHttp(this);
    [Authorize, HttpGet("{id:int}/file")]
    public async Task<IActionResult> Download(int id) => (await service.Download(id)).ToHttp(this);
}