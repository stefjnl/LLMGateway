using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;
using LLMGateway.Application.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace LLMGateway.Api.Controllers;

[ApiController]
[Route("v1/chat/completions")]
public class ChatCompletionController(KernelOrchestrator orchestrator) : ControllerBase
{
    private readonly KernelOrchestrator _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> SendCompletion(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SendChatCompletionCommand(
            request.Messages,
            request.Model,
            request.Temperature,
            request.MaxTokens);

        var response = await _orchestrator.SendChatCompletionAsync(
            command,
            cancellationToken);

        return Ok(response);
    }
}