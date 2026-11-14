using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	[DllImport("__Internal")] private static extern void poly_show_polydroid_web();
	[DllImport("__Internal")] private static extern void poly_hide_polydroid_web();
	[DllImport("__Internal")] private static extern IntPtr poly_pop_deeplink();

	private Control _root, _stickBase, _stickKnob;
	private CircleBtn _jumpBtn, _sprintBtn, _eBtn;
	private PanelContainer _launcher;
	private Button _reopenBtn;

	private int _moveIdx = -1, _lookIdx = -1;
	private Vector2 _stickCenter, _knobOffset;
	private double _timer;
	private bool _sprint;
	private const float MaxR = 64f, Dead = 0.18f;

	public override void _Ready()
	{
		Layer = 100;
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

		_reopenBtn = new Button { Text = "Polytoria", CustomMinimumSize = new Vector2(104f, 36f) };
		_reopenBtn.Pressed += OpenWeb;
		_root.AddChild(_reopenBtn);

		_launcher = new PanelContainer();
		_launcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_launcher.MouseFilter = Control.MouseFilterEnum.Stop;
		_launcher.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.071f, 0.075f, 0.094f, 1f) });
		_root.AddChild(_launcher);

		CenterContainer cc = new CenterContainer();
		_launcher.AddChild(cc);

		Button playPolyBtn = new Button { Text = "Polytoria", CustomMinimumSize = new Vector2(240f, 52f) };
		playPolyBtn.AddThemeFontSizeOverride("font_size", 18);
		playPolyBtn.AddThemeColorOverride("font_color", new Color(0.08f, 0.10f, 0.18f, 1f));
		StyleBoxFlat pill = new StyleBoxFlat { BgColor = new Color(0.706f, 0.773f, 1f, 1f) };
		pill.SetCornerRadiusAll(26);
		playPolyBtn.AddThemeStyleboxOverride("normal", pill);
		playPolyBtn.Pressed += OpenWeb;
		cc.AddChild(playPolyBtn);

		LayoutHud();
		GetViewport().SizeChanged += LayoutHud;
	}

	private void OpenWeb()
	{
		if (OS.GetName() == "iOS") poly_show_polydroid_web();
		else OS.ShellOpen("https://polytoria.com");
	}

	public override void _Process(double delta)
	{
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
		if (token.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
			token = token.Substring(12).TrimStart('/');
		if (token.StartsWith("join/", StringComparison.OrdinalIgnoreCase))
			token = token.Substring(5);

		_launcher.Visible = false;
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
		_reopenBtn.Position = new Vector2(16f, 16f);
	}

	public override void _Input(InputEvent e)
	{
		if (_launcher.Visible) return;
		if (e is InputEventScreenTouch t)
		{
			Vector2 v = GetViewport().GetVisibleRect().Size;
			if (t.Pressed)
			{
				if (t.Position.DistanceTo(_jumpBtn.Position) <= 58f) { _jumpBtn.Pressed = true; Input.ActionPress("jump"); GetViewport().SetInputAsHandled(); return; }
				if (t.Position.DistanceTo(_sprintBtn.Position) <= 38f) { _sprint = !_sprint; _sprintBtn.Pressed = _sprint; if (_sprint) Input.ActionPress("sprint"); else Input.ActionRelease("sprint"); GetViewport().SetInputAsHandled(); return; }
				if (t.Position.DistanceTo(_eBtn.Position) <= 34f) { _eBtn.Pressed = true; Input.ActionPress("interact"); GetViewport().SetInputAsHandled(); return; }
				if (_moveIdx == -1 && t.Position.X < v.X * 0.45f && t.Position.Y > 64f) { _moveIdx = t.Index; _stickCenter = t.Position; _stickBase.Position = _stickKnob.Position = _stickCenter; _stickBase.Visible = _stickKnob.Visible = true; GetViewport().SetInputAsHandled(); return; }
				if (_lookIdx == -1 && t.Position.X >= v.X * 0.45f && t.Position.Y > 64f) { _lookIdx = t.Index; GetViewport().SetInputAsHandled(); }
			}
			else
			{
				_jumpBtn.Pressed = false; Input.ActionRelease("jump");
				_eBtn.Pressed = false; Input.ActionRelease("interact");
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
				Input.ParseInputEvent(new InputEventMouseMotion { Relative = d.Relative * 2.0f });
				GetViewport().SetInputAsHandled();
			}
		}
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
