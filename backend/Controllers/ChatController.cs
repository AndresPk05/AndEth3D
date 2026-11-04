using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Authorize, Route("api/chat")]
public class ChatController(ChatService service) : ControllerBase
{
    [AllowAnonymous, HttpGet("status")]
    public IActionResult Status() => service.Status().ToHttp(this);
    [HttpGet("conversations")]
    public async Task<IActionResult> List([FromQuery, Range(0, 100000)] int skip = 0) => (await service.List(skip)).ToHttp(this);
    [HttpPost("conversations")]
    public async Task<IActionResult> Create() => (await service.Create()).ToHttp(this);
    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => (await service.Get(id)).ToHttp(this);
    [HttpPost("conversations/{id:guid}/messages"), RequestSizeLimit(16000)]
    public async Task<IActionResult> Send(Guid id, SendChatMessageRequest request) => (await service.Send(id, request)).ToHttp(this);
}