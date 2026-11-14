using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	[DllImport("__Internal")] private static extern void poly_show_polydroid_web();
	[DllImport("__Internal")] private static extern void poly_hide_polydroid_web();
	[DllImport("__Internal")] private static extern IntPtr poly_pop_deeplink();

	[ModuleInitializer]
	public static void AutoBoot()
	{
		Task.Run(async () =>
		{
			while (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
				await Task.Delay(50);
			Callable.From(() =>
			{
				if (tree.Root.GetNodeOrNull("PolyTouchControls") == null)
					tree.Root.AddChild(new PolyTouchControls { Name = "PolyTouchControls" });
			}).CallDeferred();
		});
	}

	private Control _root, _stickBase, _stickKnob;
	private CircleBtn _jumpBtn, _sprintBtn, _eBtn;
	private readonly List<CircleBtn> _customBtns = new();
	private readonly List<Key> _customKeys = new();
	private HBoxContainer _hotbarRow;
	private Button _kbdBtn, _polyTopBtn;
	private Label _fpsLabel, _sensLabel;

	private PanelContainer _launcher, _settings;
	private VBoxContainer _clientBox, _controlsBox;

	private int _moveIdx = -1, _lookIdx = -1;
	private Vector2 _stickCenter, _knobOffset;
	private double _timer;
	private bool _sprint, _showStats;
	private float _camSens = 2.0f;
	private const float MaxR = 64f, Dead = 0.18f;

	public override void _Ready()
	{
		Layer = 128;
		ProcessMode = ProcessModeEnum.Always;

		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_root);

		_stickBase = new CircleDraw(MaxR, new Color(1, 1, 1, 0.15f), new Color(1, 1, 1, 0.45f));
		_stickBase.Visible = false;
		_stickBase.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickBase);

		_stickKnob = new CircleDraw(26f, new Color(1, 1, 1, 0.48f), new Color(1, 1, 1, 0.85f));
		_stickKnob.Visible = false;
		_stickKnob.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickKnob);

		_jumpBtn = new CircleBtn(46f, "↑", 26); _jumpBtn.MouseFilter = Control.MouseFilterEnum.Ignore; _root.AddChild(_jumpBtn);
		_sprintBtn = new CircleBtn(28f, "Sprint", 12); _sprintBtn.MouseFilter = Control.MouseFilterEnum.Ignore; _root.AddChild(_sprintBtn);
		_eBtn = new CircleBtn(24f, "E", 16); _eBtn.MouseFilter = Control.MouseFilterEnum.Ignore; _root.AddChild(_eBtn);

		_kbdBtn = new Button { Text = "⌨", CustomMinimumSize = new Vector2(44f, 36f) };
		_kbdBtn.Pressed += () => { _settings.Visible = true; };
		_root.AddChild(_kbdBtn);

		_polyTopBtn = new Button { Text = "Polytoria", CustomMinimumSize = new Vector2(96f, 36f) };
		_polyTopBtn.Pressed += OpenWeb;
		_root.AddChild(_polyTopBtn);

		_fpsLabel = new Label { Visible = false };
		_fpsLabel.AddThemeFontSizeOverride("font_size", 14);
		_fpsLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.65f, 1f));
		_root.AddChild(_fpsLabel);

		// Bottom 1 2 3 Hotbar (Matches Screenshot 1)
		_hotbarRow = new HBoxContainer();
		_hotbarRow.AddThemeConstantOverride("separation", 6);
		_root.AddChild(_hotbarRow);
		for (int i = 1; i <= 3; i++)
		{
			Key k = (Key)((int)Key.Key1 + (i - 1));
			Button slot = new Button { Text = i.ToString(), CustomMinimumSize = new Vector2(44f, 44f) };
			slot.Pressed += () => SendKeyTap(k);
			_hotbarRow.AddChild(slot);
		}

		BuildLauncher();
		BuildSettings();
		LayoutHud();
		GetViewport().SizeChanged += LayoutHud;
	}

	private void BuildLauncher()
	{
		_launcher = new PanelContainer();
		_launcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_launcher.MouseFilter = Control.MouseFilterEnum.Stop;
		_launcher.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.071f, 0.075f, 0.094f, 1f) });
		_root.AddChild(_launcher);

		CenterContainer cc = new CenterContainer();
		_launcher.AddChild(cc);

		VBoxContainer stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 14);
		cc.AddChild(stack);

		Button polyBtn = new Button { Text = "Polytoria", CustomMinimumSize = new Vector2(240f, 48f) };
		polyBtn.AddThemeFontSizeOverride("font_size", 17);
		polyBtn.AddThemeColorOverride("font_color", new Color(0.08f, 0.10f, 0.18f, 1f));
		StyleBoxFlat p1 = new StyleBoxFlat { BgColor = new Color(0.706f, 0.773f, 1f, 1f) };
		p1.SetCornerRadiusAll(24);
		polyBtn.AddThemeStyleboxOverride("normal", p1);
		polyBtn.Pressed += OpenWeb;
		stack.AddChild(polyBtn);

		Button setBtn = new Button { Text = "⚙ Settings", CustomMinimumSize = new Vector2(240f, 44f) };
		setBtn.AddThemeFontSizeOverride("font_size", 15);
		setBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.87f, 0.92f, 1f));
		StyleBoxFlat p2 = new StyleBoxFlat { BgColor = new Color(0.165f, 0.176f, 0.216f, 1f) };
		p2.SetCornerRadiusAll(22);
		setBtn.AddThemeStyleboxOverride("normal", p2);
		setBtn.Pressed += () => { _settings.Visible = true; };
		stack.AddChild(setBtn);
	}
	private void BuildSettings()
	{
		_settings = new PanelContainer();
		_settings.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_settings.Visible = false;
		_settings.MouseFilter = Control.MouseFilterEnum.Stop;
		_settings.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.071f, 0.075f, 0.094f, 1f) });
		_root.AddChild(_settings);

		CenterContainer cc = new CenterContainer();
		_settings.AddChild(cc);

		VBoxContainer box = new VBoxContainer { CustomMinimumSize = new Vector2(360f, 0f) };
		box.AddThemeConstantOverride("separation", 12);
		cc.AddChild(box);

		Button back = new Button { Text = "← Settings" };
		back.AddThemeFontSizeOverride("font_size", 18);
		back.Pressed += () => { _settings.Visible = false; };
		box.AddChild(back);

		HBoxContainer tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 10);
		box.AddChild(tabs);

		Button cTab = new Button { Text = "Client", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		cTab.Pressed += () => { _clientBox.Visible = true; _controlsBox.Visible = false; };
		tabs.AddChild(cTab);

		Button kTab = new Button { Text = "Controls", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		kTab.Pressed += () => { _clientBox.Visible = false; _controlsBox.Visible = true; };
		tabs.AddChild(kTab);

		// Client Tab: Resolution, Stats Overlay, Max FPS
		_clientBox = new VBoxContainer();
		_clientBox.AddThemeConstantOverride("separation", 10);
		box.AddChild(_clientBox);

		HBoxContainer resRow = new HBoxContainer();
		resRow.AddThemeConstantOverride("separation", 8);
		_clientBox.AddChild(resRow);
		resRow.AddChild(new Label { Text = "Resolution:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		foreach (float scale in new[] { 0.75f, 1.0f })
		{
			Button rBtn = new Button { Text = scale < 1f ? "720p" : "1080p" };
			rBtn.Pressed += () => { GetViewport().Scaling3DScale = scale; };
			resRow.AddChild(rBtn);
		}

		CheckButton stats = new CheckButton { Text = "Performance stats overlay" };
		stats.Toggled += (on) => { _showStats = on; _fpsLabel.Visible = on; };
		_clientBox.AddChild(stats);

		HBoxContainer fpsRow = new HBoxContainer();
		fpsRow.AddThemeConstantOverride("separation", 8);
		_clientBox.AddChild(fpsRow);
		fpsRow.AddChild(new Label { Text = "Max FPS:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		foreach (int fps in new[] { 60, 120, 144 })
		{
			Button fBtn = new Button { Text = fps.ToString() };
			fBtn.Pressed += () => { Engine.MaxFps = fps; };
			fpsRow.AddChild(fBtn);
		}

		// Controls Tab: Camera Sensitivity & Custom Key Adder
		_controlsBox = new VBoxContainer { Visible = false };
		_controlsBox.AddThemeConstantOverride("separation", 10);
		box.AddChild(_controlsBox);

		_sensLabel = new Label { Text = "Camera sensitivity: 2.0x" };
		_controlsBox.AddChild(_sensLabel);

		HSlider slider = new HSlider { MinValue = 0.5, MaxValue = 5.0, Step = 0.1, Value = 2.0 };
		slider.ValueChanged += (v) => { _camSens = (float)v; _sensLabel.Text = "Camera sensitivity: " + _camSens.ToString("0.0") + "x"; };
		_controlsBox.AddChild(slider);

		HBoxContainer addKeyRow = new HBoxContainer();
		addKeyRow.AddThemeConstantOverride("separation", 8);
		_controlsBox.AddChild(addKeyRow);
		foreach (Key k in new[] { Key.F, Key.Q, Key.R, Key.C })
		{
			Button ak = new Button { Text = "+ " + k.ToString(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			ak.Pressed += () => AddCustomKeyButton(k);
			addKeyRow.AddChild(ak);
		}
	}

	private void AddCustomKeyButton(Key k)
	{
		if (_customKeys.Contains(k)) return;
		CircleBtn btn = new CircleBtn(24f, k.ToString(), 15);
		btn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(btn);
		_customBtns.Add(btn);
		_customKeys.Add(k);
		LayoutHud();
	}

	private void OpenWeb()
	{
		if (OS.GetName() == "iOS") poly_show_polydroid_web();
		else OS.ShellOpen("https://polytoria.com");
	}

	public override void _Process(double delta)
	{
		if (_showStats && _fpsLabel.Visible) _fpsLabel.Text = "FPS: " + Engine.GetFramesPerSecond();
		if ((_timer += delta) < 0.25 || OS.GetName() != "iOS") return;
		_timer = 0;
		try
		{
			IntPtr ptr = poly_pop_deeplink();
			if (ptr != IntPtr.Zero)
			{
				string uri = Marshal.PtrToStringUTF8(ptr) ?? "";
				Marshal.FreeHGlobal(ptr);
				if (!string.IsNullOrEmpty(uri)) LaunchGame(uri);
			}
		}
		catch { }
	}

	private void LaunchGame(string uri)
	{
		string token = uri.Trim();
		if (token.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase)) token = token.Substring(12).TrimStart('/');
		if (token.StartsWith("join/", StringComparison.OrdinalIgnoreCase)) token = token.Substring(5);

		_launcher.Visible = false;
		_settings.Visible = false;
		if (OS.GetName() == "iOS") poly_hide_polydroid_web();

		Node entry = FindEntry(GetTree().Root);
		MethodInfo m = entry?.GetType().GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
		ParameterInfo[] p = m?.GetParameters();
		if (p != null && p.Length == 1)
		{
			Type dt = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
			object obj = Activator.CreateInstance(dt);
			dt.GetField("Token")?.SetValue(obj, token);
			dt.GetProperty("Token")?.SetValue(obj, token);
			m.Invoke(entry, new[] { obj });
		}
	}

	private static Node FindEntry(Node n)
	{
		if (n.GetType().FullName == "Polytoria.Client.ClientEntry") return n;
		foreach (Node c in n.GetChildren()) { Node f = FindEntry(c); if (f != null) return f; }
		return null;
	}

	private void LayoutHud()
	{
		Vector2 v = GetViewport().GetVisibleRect().Size;
		_jumpBtn.Position = new Vector2(v.X - 96f, v.Y - 96f);
		_sprintBtn.Position = new Vector2(v.X - 180f, v.Y - 150f);
		_eBtn.Position = new Vector2(v.X - 96f, v.Y - 190f);
		_kbdBtn.Position = new Vector2(16f, 16f);
		_polyTopBtn.Position = new Vector2(68f, 16f);
		_fpsLabel.Position = new Vector2(176f, 22f);
		_hotbarRow.Position = new Vector2((v.X - 144f) * 0.5f, v.Y - 62f);
		for (int i = 0; i < _customBtns.Count; i++)
			_customBtns[i].Position = new Vector2(v.X - 180f - (i * 58f), v.Y - 215f);
	}

	public override void _Input(InputEvent e)
	{
		if (_launcher.Visible || _settings.Visible) return;
		if (e is InputEventScreenTouch t)
		{
			Vector2 v = GetViewport().GetVisibleRect().Size;
			if (t.Pressed)
			{
				if (t.Position.DistanceTo(_jumpBtn.Position) <= 58f) { _jumpBtn.Pressed = true; Input.ActionPress("jump"); GetViewport().SetInputAsHandled(); return; }
				if (t.Position.DistanceTo(_sprintBtn.Position) <= 38f) { _sprint = !_sprint; _sprintBtn.Pressed = _sprint; if (_sprint) Input.ActionPress("sprint"); else Input.ActionRelease("sprint"); GetViewport().SetInputAsHandled(); return; }
				if (t.Position.DistanceTo(_eBtn.Position) <= 34f) { _eBtn.Pressed = true; Input.ActionPress("interact"); GetViewport().SetInputAsHandled(); return; }
				for (int i = 0; i < _customBtns.Count; i++)
				{
					if (t.Position.DistanceTo(_customBtns[i].Position) <= 34f)
					{
						_customBtns[i].Pressed = true;
						SendKeyTap(_customKeys[i]);
						GetViewport().SetInputAsHandled();
						return;
					}
				}
				if (_moveIdx == -1 && t.Position.X < v.X * 0.45f && t.Position.Y > 64f) { _moveIdx = t.Index; _stickCenter = t.Position; _stickBase.Position = _stickKnob.Position = _stickCenter; _stickBase.Visible = _stickKnob.Visible = true; GetViewport().SetInputAsHandled(); return; }
				if (_lookIdx == -1 && t.Position.X >= v.X * 0.45f && t.Position.Y > 64f) { _lookIdx = t.Index; GetViewport().SetInputAsHandled(); }
			}
			else
			{
				_jumpBtn.Pressed = false; Input.ActionRelease("jump");
				_eBtn.Pressed = false; Input.ActionRelease("interact");
				foreach (CircleBtn cb in _customBtns) cb.Pressed = false;
				if (t.Index == _moveIdx) { _moveIdx = -1; _stickBase.Visible = _stickKnob.Visible = false; Input.ActionRelease("forward"); Input.ActionRelease("backward"); Input.ActionRelease("leftward"); Input.ActionRelease("rightward"); }
				else if (t.Index == _lookIdx) _lookIdx = -1;
			}
		}
		else if (e is InputEventScreenDrag d)
		{
			if (d.Index == _moveIdx)
			{
				Vector2 del = d.Position - _stickCenter;
				_knobOffset = del.Length() > MaxR ? del.Normalized() * MaxR : del;
				_stickKnob.Position = _stickCenter + _knobOffset;
				Vector2 n = _knobOffset / MaxR;
				Act("forward", n.Y < -Dead, -n.Y); Act("backward", n.Y > Dead, n.Y);
				Act("leftward", n.X < -Dead, -n.X); Act("rightward", n.X > Dead, n.X);
				GetViewport().SetInputAsHandled();
			}
			else if (d.Index == _lookIdx)
			{
				Input.ParseInputEvent(new InputEventMouseMotion { Relative = d.Relative * _camSens });
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private static void SendKeyTap(Key k)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = true });
		Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = false });
	}

	private static void Act(string a, bool on, float s) { if (on) Input.ActionPress(a, Mathf.Clamp(s, 0f, 1f)); else if (Input.IsActionPressed(a)) Input.ActionRelease(a); }

	private partial class CircleDraw : Control
	{
		private readonly float _r; private readonly Color _f, _s;
		public CircleDraw(float r, Color f, Color s) { _r = r; _f = f; _s = s; }
		public override void _Draw() { DrawCircle(Vector2.Zero, _r, _f); DrawArc(Vector2.Zero, _r, 0, Mathf.Tau, 48, _s, 2.5f, true); }
	}

	private partial class CircleBtn : Control
	{
		private readonly float _r; private readonly string _t; private readonly int _fs; private bool _p;
		public bool Pressed { get => _p; set { _p = value; QueueRedraw(); } }
		public CircleBtn(float r, string t, int fs) { _r = r; _t = t; _fs = fs; }
		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, _r, _p ? new Color(1, 1, 1, 0.42f) : new Color(0.15f, 0.16f, 0.20f, 0.55f));
			DrawArc(Vector2.Zero, _r, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.55f), 2.5f, true);
			Font f = ThemeDB.FallbackFont;
			Vector2 sz = f.GetStringSize(_t, HorizontalAlignment.Center, -1, _fs);
DrawString(f, new Vector2(-sz.X * 0.5f, sz.Y * 0.28f), _t, HorizontalAlignment.Center, -1, _fs, Colors.White);
}
}
}
