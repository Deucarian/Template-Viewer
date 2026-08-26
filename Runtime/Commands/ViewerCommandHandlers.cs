using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.ViewerAuthentication;

namespace Deucarian.TemplateViewer.Commands
{
    public static class ViewerCommandHandlers
    {
        public static IReadOnlyList<ICommandHandler<ViewerApplication>> Create(
            IViewerAuthenticationEventPublisher authenticationEventPublisher = null,
            bool includeGenericVisibilityCommands = true,
            ICommandHandler<ViewerApplication> initializationHandler = null)
        {
            var handlers = new List<ICommandHandler<ViewerApplication>>
            {
                initializationHandler ?? new InitializeViewerCommandHandler(),
                new DisposeViewerCommandHandler(),
                new ViewerAuthenticationCommandHandler<ViewerApplication>(
                    authenticationEventPublisher)
            };
            if (includeGenericVisibilityCommands)
            {
                handlers.Insert(1, new SelectViewerElementsCommandHandler());
                handlers.Insert(2, new ClearViewerSelectionCommandHandler());
            }

            return handlers;
        }
    }

    public sealed class InitializeViewerCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        public const string CommandName = "initialize_viewer";

        private static readonly IReadOnlyList<string> Names =
            new[] { CommandName };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out ViewerInitializeRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result =
                await context.Application.InitializeAsync(
                    request,
                    context.Command.Metadata.RemoteEndpoint,
                    cancellationToken);
            return ViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class SelectViewerElementsCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "select_elements" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out ViewerSelectionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result = await context.Application.SelectAsync(
                request,
                context.Command.Metadata.RemoteEndpoint,
                cancellationToken);
            return ViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class ClearViewerSelectionCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "clear_selection" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out ViewerRevisionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result = await context.Application.ClearAsync(
                request,
                context.Command.Metadata.RemoteEndpoint,
                cancellationToken);
            return ViewerCommandResultMapper.Map(result);
        }
    }

    public sealed class DisposeViewerCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        private static readonly IReadOnlyList<string> Names =
            new[] { "dispose_viewer" };

        public IReadOnlyList<string> CommandNames => Names;

        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!context.Command.TryReadPayload(
                    out ViewerRevisionRequest request,
                    out string error))
            {
                return CommandResult.Failure("invalid_payload", error);
            }

            CommandOperationResult result =
                await context.Application.DisposeViewerAsync(
                    request,
                    context.Command.Metadata.RemoteEndpoint,
                    cancellationToken);
            return ViewerCommandResultMapper.Map(result);
        }
    }

    internal static class ViewerCommandResultMapper
    {
        public static CommandResult Map(CommandOperationResult result)
        {
            return result.Succeeded
                ? CommandResult.Success(result.Payload)
                : CommandResult.Failure(result.ErrorCode, result.Message, result.Payload);
        }
    }
}
