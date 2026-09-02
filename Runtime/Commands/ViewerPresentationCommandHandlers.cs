using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.Logging;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerRendering;

namespace Deucarian.TemplateViewer.Commands
{
    internal enum ViewerNavigationCommandKind
    {
        Payload,
        Mode,
        DirectAction
    }

    internal sealed class ViewerNavigationCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        private static readonly string[] PayloadNames =
        {
            "navigation",
            "navigate",
            "nav",
            "setnavigationsensitivity",
            "set_navigation_sensitivity",
            "navigationsensitivity",
            "navigation_sensitivity"
        };

        private static readonly string[] ModeNames =
        {
            "setnavigationmode",
            "set_navigation_mode",
            "navigationmode",
            "navigation_mode"
        };

        private static readonly string[] DirectNames =
        {
            "returntoorigin",
            "return_to_origin",
            "origin",
            "home",
            "resetcamera",
            "reset_camera",
            "topdown",
            "top_down",
            "topview",
            "top_view",
            "toggletopdown",
            "toggle_top_down",
            "toggletop",
            "toggle_top",
            "orbit",
            "fly"
        };

        private static readonly DLog Log =
            DLog.For("TemplateViewer.Commands");

        private readonly ViewerNavigationController controller;
        private readonly ViewerNavigationCommandKind kind;

        public ViewerNavigationCommandHandler(
            ViewerNavigationController navigationController,
            ViewerNavigationCommandKind commandKind)
        {
            controller = navigationController;
            kind = commandKind;
        }

        public IReadOnlyList<string> CommandNames
        {
            get
            {
                switch (kind)
                {
                    case ViewerNavigationCommandKind.Mode:
                        return ModeNames;
                    case ViewerNavigationCommandKind.DirectAction:
                        return DirectNames;
                    default:
                        return PayloadNames;
                }
            }
        }

        public Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (controller == null)
            {
                return Task.FromResult(CommandResult.Failure(
                    "invalid_payload",
                    "Navigation is not initialized."));
            }

            if (!ViewerNavigationCommandPayloadParser.TryCreate(
                    context.Command,
                    context.NormalizedCommandName,
                    kind,
                    out ViewerNavigationCommand command,
                    out string parseError))
            {
                return Task.FromResult(CommandResult.Failure(
                    "invalid_payload",
                    parseError));
            }

            if (!controller.TryExecuteCommand(command, out string message))
            {
                return Task.FromResult(CommandResult.Failure(
                    "invalid_payload",
                    message));
            }

            Log.Info(message, controller);
            return Task.FromResult(CommandResult.Success());
        }
    }

    internal sealed class ViewerDisplaySettingsCommandHandler :
        ICommandHandler<ViewerApplication>
    {
        private static readonly string[] Names =
        {
            "setdisplaysettings",
            "set_display_settings"
        };

        private static readonly DLog Log =
            DLog.For("TemplateViewer.Commands");

        private readonly IViewerRenderingController controller;

        public ViewerDisplaySettingsCommandHandler(
            IViewerRenderingController renderingController)
        {
            controller = renderingController;
        }

        public IReadOnlyList<string> CommandNames => Names;

        public Task<CommandResult> HandleAsync(
            CommandExecutionContext<ViewerApplication> context,
            CancellationToken cancellationToken)
        {
            if (!ViewerDisplaySettingsCommandRequestParser.TryParse(
                    ViewerCommandPayloadAccess.Resolve(context.Command),
                    out ViewerDisplaySettingsRequest request,
                    out string error))
            {
                return Task.FromResult(CommandResult.Failure(
                    "invalid_display_settings",
                    error));
            }

            if (controller == null)
            {
                return Task.FromResult(CommandResult.Failure(
                    "invalid_display_settings",
                    "Viewer rendering is not initialized."));
            }

            controller.ApplyDisplaySettings(
                request,
                ViewerDisplaySettingsChangeSource.Host);
            ViewerDisplaySettingsSnapshot snapshot =
                controller.CurrentSettings;
            string message = "Display settings applied: rendering_mode=" +
                ViewerDisplaySettingsPayload.ToPayloadValue(
                    snapshot.RenderingMode) +
                ", camera_relative_light=" +
                snapshot.CameraRelativeLight.ToString().ToLowerInvariant() +
                ".";
            Log.Info(message);
            return Task.FromResult(CommandResult.Success());
        }
    }
}
