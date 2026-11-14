using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	[DllImport("__Internal")]
	private static extern void poly_show_polydroid_web();

	[DllImport("__Internal")]
	private static extern void poly_hide_polydroid_web();

	[DllImport("__Internal")]
	private static extern IntPtr poly_pop_deeplink();

	private Control _root;
	private Control _stickBase;
	private Control _stickKnob;
	private CircleActionBtn _jumpBtn;
	private CircleActionBtn _sprintBtn;
	private CircleActionBtn _interactBtn;
	private Button _keyboardBtn;
	private Label _fpsOverlay;

	private PanelContainer _launcherOverlay;
	private PanelContainer _settingsOverlay;
	private VBoxContainer _clientSettingsBox;
	private VBoxContainer _controlsSettingsBox;
	private Label _sensLabel;

	private int _moveTouchIndex = -1;
	private int _lookTouchIndex = -1;
	private Vector2 _stickCenter = Vector2.Zero;
	private Vector2 _knobOffset = Vector2.Zero;
	private double _pollTimer = 0.0;

	private float _cameraSensitivity = 2.0f;
	private bool _showStats = false;
	private bool _sprinting = false;

	private const float MaxRadius = 64.0f;
	private const float Deadzone = 0.18f;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;

		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_root);

		// In-Game Polydroid Touch HUD (Joystick, Jump, Sprint, E, Keyboard)
		_stickBase = new CircleControl(MaxRadius, new Color(1f, 1f, 1f, 0.14f), new Color(1f, 1f, 1f, 0.42f), 2.5f);
		_stickBase.Visible = false;
		_stickBase.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickBase);

		_stickKnob = new CircleControl(26.0f, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.80f), 2.0f);
		_stickKnob.Visible = false;
		_stickKnob.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickKnob);

		_jumpBtn = new CircleActionBtn(46.0f, "↑", 26);
		_jumpBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_jumpBtn);

		_sprintBtn = new CircleActionBtn(28.0f, "Sprint", 12);
		_sprintBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_sprintBtn);

		_interactBtn = new CircleActionBtn(24.0f, "E", 16);
		_interactBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_interactBtn);

		_keyboardBtn = new Button();
		_keyboardBtn.Text = "⌨";
		_keyboardBtn.CustomMinimumSize = new Vector2(44f, 36f);
		_keyboardBtn.Pressed += () => { _launcherOverlay.Visible = true; };
		_root.AddChild(_keyboardBtn);

		_fpsOverlay = new Label();
		_fpsOverlay.Visible = false;
		_fpsOverlay.AddThemeFontSizeOverride("font_size", 14);
		_fpsOverlay.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.65f, 1.0f));
		_root.AddChild(_fpsOverlay);

		BuildPolydroidLauncher();
		BuildPolydroidSettings();

		PositionControls();
		GetViewport().SizeChanged += PositionControls;
	}

	private void BuildPolydroidLauncher()
	{
		_launcherOverlay = new PanelContainer();
		_launcherOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_launcherOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		StyleBoxFlat bg = new StyleBoxFlat();
		bg.BgColor = new Color(0.071f, 0.075f, 0.094f, 1.0f); // Exact #121318 Polydroid dark
		_launcherOverlay.AddThemeStyleboxOverride("panel", bg);
		_root.AddChild(_launcherOverlay);

		CenterContainer center = new CenterContainer();
		_launcherOverlay.AddChild(center);

		VBoxContainer stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 14);
		center.AddChild(stack);

		// Pill 1: Periwinkle-Blue "Polytoria" Button
		Button polyBtn = new Button();
		polyBtn.Text = "Polytoria";
		polyBtn.CustomMinimumSize = new Vector2(240f, 48f);
		polyBtn.AddThemeFontSizeOverride("font_size", 17);
		polyBtn.AddThemeColorOverride("font_color", new Color(0.08f, 0.10f, 0.18f, 1.0f));
		StyleBoxFlat polyStyle = new StyleBoxFlat();
		polyStyle.BgColor = new Color(0.706f, 0.773f, 1.0f, 1.0f); // Exact #B4C5FF pill
		polyStyle.CornerRadiusTopLeft = 24;
		polyStyle.CornerRadiusTopRight = 24;
		polyStyle.CornerRadiusBottomLeft = 24;
		polyStyle.CornerRadiusBottomRight = 24;
		polyBtn.AddThemeStyleboxOverride("normal", polyStyle);
		polyBtn.Pressed += () =>
		{
			if (OS.GetName() == "iOS") poly_show_polydroid_web();
			else OS.ShellOpen("https://polytoria.com");
		};
		stack.AddChild(polyBtn);

		// Pill 2: Dark Slate "Settings" Button
		Button settingsBtn = new Button();
		settingsBtn.Text = "⚙ Settings";
		settingsBtn.CustomMinimumSize = new Vector2(240f, 44f);
		settingsBtn.AddThemeFontSizeOverride("font_size", 15);
		settingsBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.87f, 0.92f, 1.0f));
		StyleBoxFlat setStyle = new StyleBoxFlat();
		setStyle.BgColor = new Color(0.165f, 0.176f, 0.216f, 1.0f); // Exact #2A2D37 pill
		setStyle.CornerRadiusTopLeft = 22;
		setStyle.CornerRadiusTopRight = 22;
		setStyle.CornerRadiusBottomLeft = 22;
		setStyle.CornerRadiusBottomRight = 22;
		settingsBtn.AddThemeStyleboxOverride("normal", setStyle);
		settingsBtn.Pressed += () => { _settingsOverlay.Visible = true; };
		stack.AddChild(settingsBtn);
	}
	private void BuildPolydroidSettings()
	{
		_settingsOverlay = new PanelContainer();
		_settingsOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_settingsOverlay.Visible = false;
		_settingsOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		StyleBoxFlat bg = new StyleBoxFlat();
		bg.BgColor = new Color(0.071f, 0.075f, 0.094f, 1.0f);
		_settingsOverlay.AddThemeStyleboxOverride("panel", bg);
		_root.AddChild(_settingsOverlay);

		CenterContainer center = new CenterContainer();
		_settingsOverlay.AddChild(center);

		VBoxContainer panel = new VBoxContainer();
		panel.CustomMinimumSize = new Vector2(360f, 0f);
		panel.AddThemeConstantOverride("separation", 12);
		center.AddChild(panel);

		HBoxContainer header = new HBoxContainer();
		panel.AddChild(header);

		Button backBtn = new Button();
		backBtn.Text = "← Settings";
		backBtn.AddThemeFontSizeOverride("font_size", 18);
		backBtn.Pressed += () => { _settingsOverlay.Visible = false; };
		header.AddChild(backBtn);

		HBoxContainer tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 12);
		panel.AddChild(tabs);

		Button clientTab = new Button();
		clientTab.Text = "Client";
		clientTab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		clientTab.Pressed += () => { _clientSettingsBox.Visible = true; _controlsSettingsBox.Visible = false; };
		tabs.AddChild(clientTab);

		Button controlsTab = new Button();
		controlsTab.Text = "Controls";
		controlsTab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		controlsTab.Pressed += () => { _clientSettingsBox.Visible = false; _controlsSettingsBox.Visible = true; };
		tabs.AddChild(controlsTab);

		// Client Tab (Resolution, Stats Overlay, Max FPS)
		_clientSettingsBox = new VBoxContainer();
		_clientSettingsBox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(_clientSettingsBox);

		CheckButton statsToggle = new CheckButton();
		statsToggle.Text = "Performance stats overlay";
		statsToggle.Toggled += (on) => { _showStats = on; _fpsOverlay.Visible = on; };
		_clientSettingsBox.AddChild(statsToggle);

		HBoxContainer fpsRow = new HBoxContainer();
		fpsRow.AddThemeConstantOverride("separation", 8);
		_clientSettingsBox.AddChild(fpsRow);

		Label fpsLbl = new Label();
		fpsLbl.Text = "Max FPS:";
		fpsLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		fpsRow.AddChild(fpsLbl);

		foreach (int fps in new[] { 60, 120, 144 })
		{
			Button fBtn = new Button();
			fBtn.Text = fps.ToString();
			fBtn.Pressed += () => { Engine.MaxFps = fps; };
			fpsRow.AddChild(fBtn);
		}

		// Controls Tab (Camera Sensitivity Slider)
		_controlsSettingsBox = new VBoxContainer();
		_controlsSettingsBox.Visible = false;
		_controlsSettingsBox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(_controlsSettingsBox);

		_sensLabel = new Label();
		_sensLabel.Text = "Camera sensitivity: 2.0x";
		_controlsSettingsBox.AddChild(_sensLabel);

		HSlider sensSlider = new HSlider();
		sensSlider.MinValue = 0.5;
		sensSlider.MaxValue = 5.0;
		sensSlider.Step = 0.1;
		sensSlider.Value = 2.0;
		sensSlider.ValueChanged += (val) =>
		{
			_cameraSensitivity = (float)val;
			_sensLabel.Text = "Camera sensitivity: " + _cameraSensitivity.ToString("0.0") + "x";
		};
		_controlsSettingsBox.AddChild(sensSlider);
	}

	public override void _Process(double delta)
	{
		if (_showStats && _fpsOverlay.Visible)
		{
			_fpsOverlay.Text = "FPS: " + Engine.GetFramesPerSecond();
		}

		_pollTimer += delta;
		if (_pollTimer < 0.25) return;
		_pollTimer = 0.0;

		if (OS.GetName() == "iOS")
		{
			try
			{
				IntPtr ptr = poly_pop_deeplink();
				if (ptr != IntPtr.Zero)
				{
					string link = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
					Marshal.FreeHGlobal(ptr);
					if (!string.IsNullOrEmpty(link)) ConnectWithToken(link);
				}
			}
			catch { }
		}
	}

	private void ConnectWithToken(string rawUri)
	{
		string token = rawUri.Trim();
		if (token.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
		{
			token = token.Substring("polytoria://".Length).TrimStart('/');
			if (token.StartsWith("join/", StringComparison.OrdinalIgnoreCase)) token = token.Substring("join/".Length);
		}

		_launcherOverlay.Visible = false;
		_settingsOverlay.Visible = false;
		if (OS.GetName() == "iOS") poly_hide_polydroid_web();

		Node entryNode = FindClientEntryNode(GetTree().Root);
		if (entryNode == null) return;
		MethodInfo m = entryNode.GetType().GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
		if (m == null) return;
		ParameterInfo[] p = m.GetParameters();
		if (p.Length == 1)
		{
			Type dt = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
			object dataObj = Activator.CreateInstance(dt);
			FieldInfo f = dt.GetField("Token", BindingFlags.Instance | BindingFlags.Public);
			if (f != null) f.SetValue(dataObj, token);
			else
			{
				PropertyInfo pr = dt.GetProperty("Token", BindingFlags.Instance | BindingFlags.Public);
				if (pr != null && pr.CanWrite) pr.SetValue(dataObj, token);
			}
			m.Invoke(entryNode, new object[] { dataObj });
		}
	}

	private static Node FindClientEntryNode(Node node)
	{
		if (node.GetType().FullName == "Polytoria.Client.ClientEntry") return node;
		foreach (Node child in node.GetChildren())
		{
			Node found = FindClientEntryNode(child);
			if (found != null) return found;
		}
		return null;
	}

	private void PositionControls()
	{
		Vector2 v = GetViewport().GetVisibleRect().Size;
		_jumpBtn.Position = new Vector2(v.X - 96f, v.Y - 96f);
		_sprintBtn.Position = new Vector2(v.X - 180f, v.Y - 150f);
		_interactBtn.Position = new Vector2(v.X - 96f, v.Y - 190f);
		_keyboardBtn.Position = new Vector2(16f, 16f);
		_fpsOverlay.Position = new Vector2(72f, 22f);
	}

	public override void _Input(InputEvent @event)
	{
		if (_launcherOverlay.Visible || _settingsOverlay.Visible) return;

		if (@event is InputEventScreenTouch touch)
		{
			Vector2 v = GetViewport().GetVisibleRect().Size;
			if (touch.Pressed)
			{
				if (touch.Position.DistanceTo(_jumpBtn.Position) <= 58f)
				{
					_jumpBtn.PressedVisual = true;
					Input.ActionPress("jump");
					GetViewport().SetInputAsHandled();
					return;
				}
				if (touch.Position.DistanceTo(_sprintBtn.Position) <= 38f)
				{
					_sprinting = !_sprinting;
					_sprintBtn.PressedVisual = _sprinting;
					if (_sprinting) Input.ActionPress("sprint"); else Input.ActionRelease("sprint");
					GetViewport().SetInputAsHandled();
					return;
				}
				if (touch.Position.DistanceTo(_interactBtn.Position) <= 34f)
				{
					_interactBtn.PressedVisual = true;
					Input.ActionPress("interact");
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_moveTouchIndex == -1 && touch.Position.X < v.X * 0.45f && touch.Position.Y > 64f)
				{
					_moveTouchIndex = touch.Index;
					_stickCenter = touch.Position;
					_knobOffset = Vector2.Zero;
					_stickBase.Position = _stickCenter;
					_stickKnob.Position = _stickCenter;
					_stickBase.Visible = true;
					_stickKnob.Visible = true;
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_lookTouchIndex == -1 && touch.Position.X >= v.X * 0.45f && touch.Position.Y > 64f)
				{
					_lookTouchIndex = touch.Index;
					GetViewport().SetInputAsHandled();
				}
			}
			else
			{
				_jumpBtn.PressedVisual = false;
				Input.ActionRelease("jump");
				_interactBtn.PressedVisual = false;
				Input.ActionRelease("interact");

				if (touch.Index == _moveTouchIndex)
				{
					_moveTouchIndex = -1;
					_stickBase.Visible = false;
					_stickKnob.Visible = false;
					Input.ActionRelease("forward");
					Input.ActionRelease("backward");
					Input.ActionRelease("leftward");
					Input.ActionRelease("rightward");
				}
				else if (touch.Index == _lookTouchIndex)
				{
					_lookTouchIndex = -1;
				}
			}
		}
		else if (@event is InputEventScreenDrag drag)
		{
			if (drag.Index == _moveTouchIndex)
			{
				Vector2 d = drag.Position - _stickCenter;
				if (d.Length() > MaxRadius) d = d.Normalized() * MaxRadius;
				_knobOffset = d;
				_stickKnob.Position = _stickCenter + _knobOffset;
				Vector2 n = _knobOffset / MaxRadius;
				SetAct("forward", n.Y < -Deadzone, Mathf.Clamp(-n.Y, 0f, 1f));
				SetAct("backward", n.Y > Deadzone, Mathf.Clamp(n.Y, 0f, 1f));
				SetAct("leftward", n.X < -Deadzone, Mathf.Clamp(-n.X, 0f, 1f));
				SetAct("rightward", n.X > Deadzone, Mathf.Clamp(n.X, 0f, 1f));
				GetViewport().SetInputAsHandled();
			}
			else if (drag.Index == _lookTouchIndex)
			{
				InputEventMouseMotion mm = new InputEventMouseMotion();
				mm.Relative = drag.Relative * _cameraSensitivity;
				Input.ParseInputEvent(mm);
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private static void SetAct(string a, bool on, float s)
	{
		if (on) Input.ActionPress(a, s);
		else if (Input.IsActionPressed(a)) Input.ActionRelease(a);
	}

	private partial class CircleControl : Control
	{
		private readonly float _r;
		private readonly Color _f, _s;
		private readonly float _w;
		public CircleControl(float r, Color f, Color s, float w) { _r = r; _f = f; _s = s; _w = w; }
		public override void _Draw() { DrawCircle(Vector2.Zero, _r, _f); DrawArc(Vector2.Zero, _r, 0f, Mathf.Tau, 48, _s, _w, true); }
	}

	private partial class CircleActionBtn : Control
	{
		private readonly float _r;
		private readonly string _lbl;
		private readonly int _fs;
		private bool _p;
		public bool PressedVisual { get => _p; set { _p = value; QueueRedraw(); } }
		public CircleActionBtn(float r, string lbl, int fs) { _r = r; _lbl = lbl; _fs = fs; }
		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, _r, _p ? new Color(1f, 1f, 1f, 0.42f) : new Color(0.15f, 0.16f, 0.20f, 0.55f));
			DrawArc(Vector2.Zero, _r, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.55f), 2.5f, true);
			Font f = ThemeDB.FallbackFont;
			Vector2 sz = f.GetStringSize(_lbl, HorizontalAlignment.Center, -1, _fs);
			DrawString(f, new Vector2(-sz.X * 0.5f, sz.Y * 0.28f), _lbl, HorizontalAlignment.Center, -1, _fs, Colors.White);
		}
	}
}
