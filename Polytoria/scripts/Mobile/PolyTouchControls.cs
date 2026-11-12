using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	private Control _root;
	private Control _stickBase;
	private Control _stickKnob;
	private Control _jumpButton;

	private int _moveTouchIndex = -1;
	private int _jumpTouchIndex = -1;
	private Vector2 _stickCenter = Vector2.Zero;
	private Vector2 _knobOffset = Vector2.Zero;

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

		PositionJumpButton();
		GetViewport().SizeChanged += PositionJumpButton;
	}

	private void PositionJumpButton()
	{
		Vector2 viewSize = GetViewport().GetVisibleRect().Size;
		_jumpButton.Position = new Vector2(viewSize.X - 110.0f, viewSize.Y - 110.0f);
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

