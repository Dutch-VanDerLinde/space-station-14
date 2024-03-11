using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Content.Shared.Roles;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Preferences.UI;

public abstract class RequirementsSelector<T> : BoxContainer where T : IPrototype
{
    private ButtonGroup _loadoutGroup;

    public T Proto { get; }
    public bool Disabled => _lockStripe.Visible;

    protected readonly RadioOptions<int> Options;
    private readonly StripeBack _lockStripe;
    private LoadoutWindow? _loadoutWindow;

    private RoleLoadout? _loadout;

    /// <summary>
    /// Raised if a loadout has been updated.
    /// </summary>
    public event Action<RoleLoadout>? LoadoutUpdated;

    protected RequirementsSelector(T proto, ButtonGroup loadoutGroup)
    {
        _loadoutGroup = loadoutGroup;
        Proto = proto;

        Options = new RadioOptions<int>(RadioOptionsLayout.Horizontal)
        {
            FirstButtonStyle = StyleBase.ButtonOpenRight,
            ButtonStyle = StyleBase.ButtonOpenBoth,
            LastButtonStyle = StyleBase.ButtonOpenLeft
        };
        //Override default radio option button width
        Options.GenerateItem = GenerateButton;

        Options.OnItemSelected += args => Options.Select(args.Id);

        var requirementsLabel = new Label()
        {
            Text = Loc.GetString("role-timer-locked"),
            Visible = true,
            HorizontalAlignment = HAlignment.Center,
            StyleClasses = {StyleBase.StyleClassLabelSubText},
        };

        _lockStripe = new StripeBack()
        {
            Visible = false,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Stop,
            Children =
            {
                requirementsLabel
            }
        };

        // Setup must be called after
    }

    /// <summary>
    /// Actually adds the controls, must be called in the inheriting class' constructor.
    /// </summary>
    protected void Setup(RoleLoadout? loadout, (string, int)[] items, string title, int titleSize, string? description, TextureRect? icon = null)
    {
        _loadout = loadout;

        foreach (var (text, value) in items)
        {
            Options.AddItem(Loc.GetString(text), value);
        }

        var titleLabel = new Label()
        {
            Margin = new Thickness(5f, 0, 5f, 0),
            Text = title,
            MinSize = new Vector2(titleSize, 0),
            MouseFilter = MouseFilterMode.Stop,
            ToolTip = description
        };

        if (icon != null)
            AddChild(icon);

        AddChild(titleLabel);
        AddChild(Options);
        AddChild(_lockStripe);

        var loadoutWindowBtn = new Button()
        {
            Text = Loc.GetString("loadout-window"),
            HorizontalExpand = true,
            Group = _loadoutGroup,
        };

        var collection = IoCManager.Instance!;
        var entManager = collection.Resolve<IEntityManager>();
        var protoManager = collection.Resolve<IPrototypeManager>();

        // If no loadout found then disabled button
        if (!protoManager.HasIndex<RoleLoadoutPrototype>("Job" + Proto.ID))
        {
            loadoutWindowBtn.Disabled = true;
        }
        // else
        else
        {
            var session = collection.Resolve<IPlayerManager>().LocalSession!;
            // TODO: Most of lobby state should be a uicontroller
            // trying to handle all this shit is a big-ass mess.
            // Every time I touch it I try to make it slightly better but it needs a howitzer dropped on it.
            loadoutWindowBtn.OnPressed += args =>
            {
                if (args.Button.Pressed)
                {
                    // We only create a loadout when necessary to avoid unnecessary DB entries.
                    _loadout ??= new RoleLoadout("Job" + Proto.ID);
                    _loadout.SetDefault(entManager, protoManager);

                    _loadoutWindow = new LoadoutWindow(_loadout, protoManager.Index(_loadout.Role), session, collection)
                    {
                        Title = Loc.GetString(Proto.ID + "-loadout"),
                    };

                    _loadoutWindow.RefreshLoadouts(_loadout, session, collection);

                    // If it's a job preview then refresh it.
                    if (Proto is JobPrototype jobProto)
                    {
                        var controller = UserInterfaceManager.GetUIController<LobbyUIController>();
                        controller.SetDummyJob(jobProto, _loadout);
                    }

                    _loadoutWindow.OnLoadoutPressed += (selectedGroup, selectedLoadout) =>
                    {
                        _loadout.ApplyLoadout(selectedGroup, selectedLoadout, entManager);
                        _loadoutWindow.RefreshLoadouts(_loadout, session, collection);
                        var controller = UserInterfaceManager.GetUIController<LobbyUIController>();
                        controller.UpdateCharacterUI();
                        LoadoutUpdated?.Invoke(_loadout);
                    };

                    _loadoutWindow.OpenCenteredLeft();
                    _loadoutWindow.OnClose += () =>
                    {
                        loadoutWindowBtn.Pressed = false;
                    };
                }
                else
                {
                    CloseLoadout();
                }
            };
        }

        AddChild(loadoutWindowBtn);
    }

    public void CloseLoadout()
    {
        _loadoutWindow?.Close();
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
    }

    public void LockRequirements(FormattedMessage requirements)
    {
        var tooltip = new Tooltip();
        tooltip.SetMessage(requirements);
        _lockStripe.TooltipSupplier = _ => tooltip;
        _lockStripe.Visible = true;
        Options.Visible = false;
    }

    // TODO: Subscribe to roletimers event. I am too lazy to do this RN But I doubt most people will notice fn
    public void UnlockRequirements()
    {
        _lockStripe.Visible = false;
        Options.Visible = true;
    }

    private Button GenerateButton(string text, int value)
    {
        return new Button
        {
            Text = text,
            MinWidth = 90
        };
    }
}