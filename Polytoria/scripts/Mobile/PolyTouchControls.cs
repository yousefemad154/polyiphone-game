using System;
using System.Reflection;
using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	private Control _root;
	private Control _stickBase;
	private Control _stickKnob;
	private Control _jumpButton;
	private Button _pasteJoinButton;
	private Label _statusLabel;

	private int _moveTouchIndex = -1;
	private int _jumpTouchIndex = -1;
	private Vector2 _stickCenter = Vector2.Zero;
	private Vector2 _knobOffset = Vector2.Zero;
	private string _lastHandledUri = string.Empty;

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

		BuildJoinBar();

		PositionControls();
		GetViewport().SizeChanged += PositionControls;

		CallDeferred(nameof(TryClipboardConnect), false);
	}

	private void BuildJoinBar()
	{
		_pasteJoinButton = new Button();
		_pasteJoinButton.Text = "Paste & Join";
		_pasteJoinButton.CustomMinimumSize = new Vector2(150f, 44f);
		_pasteJoinButton.AddThemeFontSizeOverride("font_size", 16);
		_pasteJoinButton.Pressed += () => TryClipboardConnect(true);
		_root.AddChild(_pasteJoinButton);

		_statusLabel = new Label();
		_statusLabel.Text = "";
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_statusLabel.AddThemeFontSizeOverride("font_size", 13);
		_statusLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.9f));
		_statusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_statusLabel);
	}

	private void PositionControls()
	{
		Vector2 viewSize = GetViewport().GetVisibleRect().Size;
		_jumpButton.Position = new Vector2(viewSize.X - 110.0f, viewSize.Y - 110.0f);
		_pasteJoinButton.Position = new Vector2(viewSize.X - 175.0f, 24.0f);
		_statusLabel.Position = new Vector2(viewSize.X - 360.0f, 74.0f);
		_statusLabel.Size = new Vector2(335.0f, 28.0f);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusIn || what == NotificationApplicationResumed)
		{
			CallDeferred(nameof(TryClipboardConnect), false);
		}
	}

	private void TryClipboardConnect(bool manualTap)
	{
		string clip = DisplayServer.ClipboardGet()?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(clip))
		{
			if (manualTap) ShowStatus("Clipboard is empty. Copy polytoria:// link first.");
			return;
		}

		string uri = ExtractPolytoriaUri(clip);
		if (string.IsNullOrEmpty(uri))
		{
			if (manualTap) ShowStatus("No polytoria:// link found in clipboard.");
			return;
		}

		if (!manualTap && uri == _lastHandledUri)
		{
			return;
		}

		_lastHandledUri = uri;
		ShowStatus("Joining server...");
		ForwardDeepLinkToPolytoria(uri);
	}

	private static string ExtractPolytoriaUri(string text)
	{
		int idx = text.IndexOf("polytoria://", StringComparison.OrdinalIgnoreCase);
		if (idx >= 0)
		{
			string sub = text.Substring(idx).Trim();
			int end = sub.IndexOfAny(new[] { ' ', '\n', '\r', '\t', '"', '\'' });
			return end > 0 ? sub.Substring(0, end) : sub;
		}

		if (text.Length > 20 && !text.Contains(" ") && !text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return "polytoria://" + text;
		}

		return string.Empty;
	}

	private void ForwardDeepLinkToPolytoria(string uri)
	{
		Node root = GetTree().Root;
		bool dispatched = DispatchRecursive(root, uri);
		if (dispatched)
		{
			_pasteJoinButton.Visible = false;
			ShowStatus("Connected!");
		}
		else
		{
			ShowStatus("Sent join link to client.");
		}
	}

	private bool DispatchRecursive(Node node, string uri)
	{
		bool handled = false;

		if (node.HasSignal("deeplink_received"))
		{
			node.EmitSignal("deeplink_received", uri);
			handled = true;
		}

		string[] candidateMethods =
		{
			"OnDeeplinkReceived",
			"_on_deeplink_received",
			"HandleDeeplink",
			"ProcessDeeplink",
			"ParseLaunchArgs",
			"ConnectFromUri",
			"JoinServer"
		};

		foreach (string method in candidateMethods)
		{
			if (node.HasMethod(method))
			{
				node.Call(method, uri);
				handled = true;
			}
		}

		Type type = node.GetType();
		if (type.Namespace != null && type.Namespace.StartsWith("Polytoria"))
		{
			foreach (MethodInfo mi in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				string name = mi.Name.ToLowerInvariant();
				ParameterInfo[] pars = mi.GetParameters();
				if (pars.Length == 1 && pars.ParameterType == typeof(string) &&
					(name.Contains("deeplink") || name.Contains("launch") || name.Contains("protocol") || name.Contains("uri")))
				{
					try
					{
						mi.Invoke(node, new object[] { uri });
						handled = true;
					}
					catch { }
				}
			}
		}

		foreach (Node child in node.GetChildren())
		{
			if (DispatchRecursive(child, uri))
			{
				handled = true;
			}
		}

		return handled;
	}

	private void ShowStatus(string msg)
	{
		_statusLabel.Text = msg;
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

				if (_moveTouchIndex == -1 && touch.Position.X < viewSize.X * 0.48f)
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
		else if (@event is InputEventScreenDrag drag)
		{
			if (drag.Index == _moveTouchIndex)
			{
				Vector2 delta = drag.Position - _stickCenter;
				if (delta.Length() > MaxRadius)
				{
					delta = delta.Normalized() * MaxRadius;
				}
				_knobOffset = delta;
				_stickKnob.Position = _stickCenter + _knobOffset;
				ApplyMovementActions(_knobOffset / MaxRadius);
				GetViewport().SetInputAsHandled();
			}
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
		if (active)
		{
			Input.ActionPress(action, strength);
		}
		else if (Input.IsActionPressed(action))
		{
			Input.ActionRelease(action);
		}
	}

	private partial class CircleControl : Control
	{
		private readonly float _radius;
		private readonly Color _fill;
		private readonly Color _stroke;
		private readonly float _width;

		public CircleControl(float radius, Color fill, Color stroke, float width)
		{
			_radius = radius;
			_fill = fill;
			_stroke = stroke;
			_width = width;
		}

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, _radius, _fill);
			DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, _stroke, _width, true);
		}
	}

	private partial class JumpButtonControl : Control
	{
		private readonly float _radius;
		private bool _pressed;

		public bool IsPressedVisual
		{
			get => _pressed;
			set
			{
				_pressed = value;
				QueueRedraw();
			}
		}

		public JumpButtonControl(float radius)
		{
			_radius = radius;
		}

		public override void _Draw()
		{
			Color fill = _pressed ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.22f);
			Color stroke = new Color(1f, 1f, 1f, 0.65f);
			DrawCircle(Vector2.Zero, _radius, fill);
			DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, stroke, 3.0f, true);

			Vector2[] arrow = new Vector2[]
			{
				new Vector2(0, -18),
				new Vector2(15, 4),
				new Vector2(6, 4),
				new Vector2(6, 18),
				new Vector2(-6, 18),
				new Vector2(-6, 4),
				new Vector2(-15, 4)
			};
			DrawColoredPolygon(arrow, new Color(1f, 1f, 1f, 0.85f));
		}
	}
}
