using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Authorize, Route("api/chat/conversations/{conversationId:guid}/visuals")]
public class VisualProposalsController(VisualProposalsService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid conversationId) => (await service.List(conversationId)).ToHttp(this);
    [HttpPost, RequestSizeLimit(4000)]
    public async Task<IActionResult> Create(Guid conversationId, CreateVisualProposalRequest request) =>
        (await service.Create(conversationId, request)).ToHttp(this, acceptedLocation: $"/api/chat/conversations/{conversationId}/visuals");
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid conversationId, Guid id) => (await service.Image(conversationId, id)).ToHttp(this);
}