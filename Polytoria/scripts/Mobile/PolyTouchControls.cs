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
	private Control _jumpButton;

	private Button _openWebBtn;
	private LineEdit _serverInput;
	private Button _joinServerBtn;

	private int _moveTouchIndex = -1;
	private int _jumpTouchIndex = -1;
	private Vector2 _stickCenter = Vector2.Zero;
	private Vector2 _knobOffset = Vector2.Zero;
	private double _pollTimer = 0.0;

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

		_stickBase = new CircleControl(MaxRadius, new Color(1f, 1f, 1f, 0.18f), new Color(1f, 1f, 1f, 0.45f), 3.0f);
		_stickBase.Visible = false;
		_stickBase.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickBase);

		_stickKnob = new CircleControl(26.0f, new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.85f), 2.0f);
		_stickKnob.Visible = false;
		_stickKnob.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_stickKnob);

		_jumpButton = new JumpButtonControl(46.0f);
		_jumpButton.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_jumpButton);

		_openWebBtn = new Button();
		_openWebBtn.Text = "Polytoria Web";
		_openWebBtn.CustomMinimumSize = new Vector2(124f, 36f);
		_openWebBtn.Pressed += () => { if (OS.GetName() == "iOS") poly_show_polydroid_web(); };
		_root.AddChild(_openWebBtn);

		_serverInput = new LineEdit();
		_serverInput.PlaceholderText = "Private IP:Port";
		_serverInput.CustomMinimumSize = new Vector2(170f, 36f);
		_root.AddChild(_serverInput);

		_joinServerBtn = new Button();
		_joinServerBtn.Text = "Join IP";
		_joinServerBtn.CustomMinimumSize = new Vector2(76f, 36f);
		_joinServerBtn.Pressed += OnJoinPrivateServer;
		_root.AddChild(_joinServerBtn);

		PositionControls();
		GetViewport().SizeChanged += PositionControls;

		if (OS.GetName() == "iOS")
		{
			CallDeferred(nameof(OpenWebDelayed));
		}
	}

	private void OpenWebDelayed()
	{
		if (OS.GetName() == "iOS") poly_show_polydroid_web();
	}

	public override void _Process(double delta)
	{
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
					if (!string.IsNullOrEmpty(link))
					{
						ConnectWithToken(link);
					}
				}
			}
			catch { }
		}
	}
	private void OnJoinPrivateServer()
	{
		string raw = _serverInput.Text.Trim();
		if (string.IsNullOrEmpty(raw)) return;

		if (OS.GetName() == "iOS") poly_hide_polydroid_web();

		string host = raw;
		int port = 24221;
		int colon = raw.LastIndexOf(':');
		if (colon > 0 && colon < raw.Length - 1)
		{
			host = raw.Substring(0, colon).Trim();
			int.TryParse(raw.Substring(colon + 1).Trim(), out port);
		}

		Node entryNode = FindClientEntryNode(GetTree().Root);
		if (entryNode == null) return;
		MethodInfo m = entryNode.GetType().GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
		if (m == null) return;
		ParameterInfo[] p = m.GetParameters();
		if (p.Length == 1)
		{
			Type dt = Nullable.GetUnderlyingType(p[0].ParameterType) ?? p[0].ParameterType;
			object dataObj = Activator.CreateInstance(dt);
			SetField(dt, dataObj, "TestIsServer", false);
			SetField(dt, dataObj, "ConnectAddress", host);
			SetField(dt, dataObj, "ConnectPort", port);
			SetField(dt, dataObj, "TestUserID", 1144);
			m.Invoke(entryNode, new object[] { dataObj });
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

		if (OS.GetName() == "iOS") poly_hide_polydroid_web();

		Node entryNode = FindClientEntryNode(GetTree().Root);
		if (entryNode == null) return;
		MethodInfo m = entryNode.GetType().GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
		if (m == null) return;
		ParameterInfo[] p = m.GetParameters();
		if (p.Length == 1)
		{
			Type dt = Nullable.GetUnderlyingType(p[0].ParameterType) ?? p[0].ParameterType;
			object dataObj = Activator.CreateInstance(dt);
			SetField(dt, dataObj, "Token", token);
			m.Invoke(entryNode, new object[] { dataObj });
		}
	}

	private static void SetField(Type t, object target, string name, object val)
	{
		FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
		if (f != null) { f.SetValue(target, val); return; }
		PropertyInfo pr = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
		if (pr != null && pr.CanWrite) pr.SetValue(target, val);
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
		Vector2 viewSize = GetViewport().GetVisibleRect().Size;
		_jumpButton.Position = new Vector2(viewSize.X - 110.0f, viewSize.Y - 110.0f);
		_openWebBtn.Position = new Vector2(16.0f, 16.0f);
		_serverInput.Position = new Vector2(148.0f, 16.0f);
		_joinServerBtn.Position = new Vector2(324.0f, 16.0f);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventScreenTouch touch)
		{
			Vector2 viewSize = GetViewport().GetVisibleRect().Size;
			if (touch.Pressed)
			{
				if (_jumpTouchIndex == -1 && touch.Position.DistanceTo(_jumpButton.Position) <= 68.0f)
				{
					_jumpTouchIndex = touch.Index;
					((JumpButtonControl)_jumpButton).IsPressedVisual = true;
					Input.ActionPress("jump");
					GetViewport().SetInputAsHandled();
					return;
				}
				if (_moveTouchIndex == -1 && touch.Position.X < viewSize.X * 0.48f && touch.Position.Y > 64.0f)
				{
					_moveTouchIndex = touch.Index;
					_stickCenter = touch.Position;
					_knobOffset = Vector2.Zero;
					_stickBase.Position = _stickCenter;
					_stickKnob.Position = _stickCenter;
					_stickBase.Visible = true;
					_stickKnob.Visible = true;
					GetViewport().SetInputAsHandled();
				}
			}
			else
			{
				if (touch.Index == _jumpTouchIndex)
				{
					_jumpTouchIndex = -1;
					((JumpButtonControl)_jumpButton).IsPressedVisual = false;
					Input.ActionRelease("jump");
					GetViewport().SetInputAsHandled();
				}
				else if (touch.Index == _moveTouchIndex)
				{
					_moveTouchIndex = -1;
					_knobOffset = Vector2.Zero;
					_stickBase.Visible = false;
					_stickKnob.Visible = false;
					ReleaseMovementActions();
					GetViewport().SetInputAsHandled();
				}
			}
		}
		else if (@event is InputEventScreenDrag drag && drag.Index == _moveTouchIndex)
		{
			Vector2 delta = drag.Position - _stickCenter;
			if (delta.Length() > MaxRadius) delta = delta.Normalized() * MaxRadius;
			_knobOffset = delta;
			_stickKnob.Position = _stickCenter + _knobOffset;
			ApplyMovementActions(_knobOffset / MaxRadius);
			GetViewport().SetInputAsHandled();
		}
	}

	private void ApplyMovementActions(Vector2 norm)
	{
		SetActionState("forward", norm.Y < -Deadzone, Mathf.Clamp(-norm.Y, 0f, 1f));
		SetActionState("backward", norm.Y > Deadzone, Mathf.Clamp(norm.Y, 0f, 1f));
		SetActionState("leftward", norm.X < -Deadzone, Mathf.Clamp(-norm.X, 0f, 1f));
		SetActionState("rightward", norm.X > Deadzone, Mathf.Clamp(norm.X, 0f, 1f));
	}

	private void ReleaseMovementActions()
	{
		Input.ActionRelease("forward");
		Input.ActionRelease("backward");
		Input.ActionRelease("leftward");
		Input.ActionRelease("rightward");
	}

	private void SetActionState(string action, bool active, float strength)
	{
		if (active) Input.ActionPress(action, strength);
		else if (Input.IsActionPressed(action)) Input.ActionRelease(action);
	}

	private partial class CircleControl : Control
	{
		private readonly float _radius;
		private readonly Color _fill;
		private readonly Color _stroke;
		private readonly float _width;
		public CircleControl(float radius, Color fill, Color stroke, float width) { _radius = radius; _fill = fill; _stroke = stroke; _width = width; }
		public override void _Draw() { DrawCircle(Vector2.Zero, _radius, _fill); DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, _stroke, _width, true); }
	}

	private partial class JumpButtonControl : Control
	{
		private readonly float _radius;
		private bool _pressed;
		public bool IsPressedVisual { get => _pressed; set { _pressed = value; QueueRedraw(); } }
		public JumpButtonControl(float radius) { _radius = radius; }
		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, _radius, _pressed ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.22f));
			DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.65f), 3.0f, true);
			DrawColoredPolygon(new Vector2[] { new(0, -18), new(15, 4), new(6, 4), new(6, 18), new(-6, 18), new(-6, 4), new(-15, 4) }, new Color(1f, 1f, 1f, 0.85f));
		}
	}
}
