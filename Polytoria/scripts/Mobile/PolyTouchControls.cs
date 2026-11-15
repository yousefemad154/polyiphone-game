using System;
using System.Collections.Generic;
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
	private readonly List<CircleBtn> _customBtns = new();
	private readonly List<Key> _customKeys = new();
	private HBoxContainer _hotbarRow;
	private Button _kbdBtn, _polyTopBtn;
	private Label _fpsLabel, _sensLabel;
	private PanelContainer _launcher, _settings;
	private VBoxContainer _clientBox, _controlsBox;

	private int _moveIdx = -1, _lookIdx = -1, _jumpIdx = -1, _eIdx = -1;
	private Vector2 _stickCenter, _knobOffset;
	private double _timer;
	private bool _sprint, _showStats;
	private float _camSens = 2.0f;

	private const float MaxR = 64f;
	private const float Dead = 0.18f;

	public override void _Ready()
	{
		Layer = 128;
		ProcessMode = ProcessModeEnum.Always;
		Input.MouseMode = Input.MouseModeEnum.Visible;

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

		_jumpBtn = new CircleBtn(46f, "↑", 26);
		_jumpBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_jumpBtn);

		_sprintBtn = new CircleBtn(28f, "Sprint", 12);
		_sprintBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_sprintBtn);

		_eBtn = new CircleBtn(24f, "E", 16);
		_eBtn.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(_eBtn);

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

		_hotbarRow = new HBoxContainer();
		_hotbarRow.AddThemeConstantOverride("separation", 6);
		_root.AddChild(_hotbarRow);

		for (int i = 1; i <= 3; i++)
		{
			Key key = (Key)((int)Key.Key1 + (i - 1));
			Button slot = new Button
			{
				Text = i.ToString(),
				CustomMinimumSize = new Vector2(44f, 44f)
			};
			slot.Pressed += () => SendKeyTap(key);
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
		_launcher.AddThemeStyleboxOverride(
			"panel",
			new StyleBoxFlat { BgColor = new Color(0.071f, 0.075f, 0.094f, 1f) }
		);
		_root.AddChild(_launcher);

		CenterContainer center = new CenterContainer();
		_launcher.AddChild(center);

		VBoxContainer stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 14);
		center.AddChild(stack);

		Button polyButton = new Button
		{
			Text = "Polytoria",
			CustomMinimumSize = new Vector2(240f, 48f)
		};
		polyButton.AddThemeFontSizeOverride("font_size", 17);
		polyButton.AddThemeColorOverride("font_color", new Color(0.08f, 0.10f, 0.18f, 1f));

		StyleBoxFlat polyStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.706f, 0.773f, 1f, 1f)
		};
		polyStyle.SetCornerRadiusAll(24);
		polyButton.AddThemeStyleboxOverride("normal", polyStyle);
		polyButton.Pressed += OpenWeb;
		stack.AddChild(polyButton);

		Button settingsButton = new Button
		{
			Text = "⚙ Settings",
			CustomMinimumSize = new Vector2(240f, 44f)
		};
		settingsButton.AddThemeFontSizeOverride("font_size", 15);
		settingsButton.AddThemeColorOverride("font_color", new Color(0.85f, 0.87f, 0.92f, 1f));

		StyleBoxFlat settingsStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.165f, 0.176f, 0.216f, 1f)
		};
		settingsStyle.SetCornerRadiusAll(22);
		settingsButton.AddThemeStyleboxOverride("normal", settingsStyle);
		settingsButton.Pressed += () => { _settings.Visible = true; };
		stack.AddChild(settingsButton);
	}

	private void BuildSettings()
	{
		_settings = new PanelContainer();
		_settings.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_settings.Visible = false;
		_settings.MouseFilter = Control.MouseFilterEnum.Stop;
		_settings.AddThemeStyleboxOverride(
			"panel",
			new StyleBoxFlat { BgColor = new Color(0.071f, 0.075f, 0.094f, 1f) }
		);
		_root.AddChild(_settings);

		CenterContainer center = new CenterContainer();
		_settings.AddChild(center);

		VBoxContainer box = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(360f, 0f)
		};
		box.AddThemeConstantOverride("separation", 12);
		center.AddChild(box);

		Button backButton = new Button { Text = "← Settings" };
		backButton.AddThemeFontSizeOverride("font_size", 18);
		backButton.Pressed += () => { _settings.Visible = false; };
		box.AddChild(backButton);

		HBoxContainer tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 10);
		box.AddChild(tabs);

		Button clientTab = new Button
		{
			Text = "Client",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		clientTab.Pressed += () =>
		{
			_clientBox.Visible = true;
			_controlsBox.Visible = false;
		};
		tabs.AddChild(clientTab);

		Button controlsTab = new Button
		{
			Text = "Controls",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		controlsTab.Pressed += () =>
		{
			_clientBox.Visible = false;
			_controlsBox.Visible = true;
		};
		tabs.AddChild(controlsTab);

		_clientBox = new VBoxContainer();
		_clientBox.AddThemeConstantOverride("separation", 10);
		box.AddChild(_clientBox);

		HBoxContainer resolutionRow = new HBoxContainer();
		resolutionRow.AddThemeConstantOverride("separation", 8);
		_clientBox.AddChild(resolutionRow);
		resolutionRow.AddChild(new Label
		{
			Text = "Resolution:",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		});

		foreach (float scale in new[] { 0.75f, 1.0f })
		{
			Button resolutionButton = new Button
			{
				Text = scale < 1f ? "720p" : "1080p"
			};
			resolutionButton.Pressed += () => { GetViewport().Scaling3DScale = scale; };
			resolutionRow.AddChild(resolutionButton);
		}

		CheckButton statsButton = new CheckButton
		{
			Text = "Performance stats overlay"
		};
		statsButton.Toggled += (enabled) =>
		{
			_showStats = enabled;
			_fpsLabel.Visible = enabled;
		};
		_clientBox.AddChild(statsButton);

		HBoxContainer fpsRow = new HBoxContainer();
		fpsRow.AddThemeConstantOverride("separation", 8);
		_clientBox.AddChild(fpsRow);
		fpsRow.AddChild(new Label
		{
			Text = "Max FPS:",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		});

		foreach (int fps in new[] { 60, 120, 144 })
		{
			Button fpsButton = new Button { Text = fps.ToString() };
			fpsButton.Pressed += () => { Engine.MaxFps = fps; };
			fpsRow.AddChild(fpsButton);
		}

		_controlsBox = new VBoxContainer { Visible = false };
		_controlsBox.AddThemeConstantOverride("separation", 10);
		box.AddChild(_controlsBox);

		_sensLabel = new Label { Text = "Camera sensitivity: 2.0x" };
		_controlsBox.AddChild(_sensLabel);

		HSlider sensitivitySlider = new HSlider
		{
			MinValue = 0.5,
			MaxValue = 5.0,
			Step = 0.1,
			Value = 2.0
		};
		sensitivitySlider.ValueChanged += (value) =>
		{
			_camSens = (float)value;
			_sensLabel.Text = "Camera sensitivity: " + _camSens.ToString("0.0") + "x";
		};
		_controlsBox.AddChild(sensitivitySlider);

		HBoxContainer addKeyRow = new HBoxContainer();
		addKeyRow.AddThemeConstantOverride("separation", 8);
		_controlsBox.AddChild(addKeyRow);

		foreach (Key key in new[] { Key.F, Key.Q, Key.R, Key.C })
		{
			Button addKeyButton = new Button
			{
				Text = "+ " + key,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			addKeyButton.Pressed += () => AddCustomKeyButton(key);
			addKeyRow.AddChild(addKeyButton);
		}
	}

	private void AddCustomKeyButton(Key key)
	{
		if (_customKeys.Contains(key))
			return;

		CircleBtn button = new CircleBtn(24f, key.ToString(), 15);
		button.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(button);
		_customBtns.Add(button);
		_customKeys.Add(key);
		LayoutHud();
	}

	private void OpenWeb()
	{
		if (OS.GetName() == "iOS")
			poly_show_polydroid_web();
		else
			OS.ShellOpen("https://polytoria.com");
	}

	public override void _Process(double delta)
	{
		if (_launcher.Visible || _settings.Visible)
			Input.MouseMode = Input.MouseModeEnum.Visible;

		if (_showStats && _fpsLabel.Visible)
			_fpsLabel.Text = "FPS: " + Engine.GetFramesPerSecond();

		if ((_timer += delta) < 0.25 || OS.GetName() != "iOS")
			return;

		_timer = 0;

		try
		{
			IntPtr pointer = poly_pop_deeplink();
			if (pointer != IntPtr.Zero)
			{
				string uri = Marshal.PtrToStringUTF8(pointer) ?? "";
				Marshal.FreeHGlobal(pointer);

				if (!string.IsNullOrEmpty(uri))
					LaunchGame(uri);
			}
		}
		catch
		{
			// Native deeplink polling can fail when the iOS plugin is unavailable.
		}
	}

	private void LaunchGame(string uri)
	{
		string token = uri.Trim();

		if (token.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
			token = token.Substring(12).TrimStart('/');

		if (token.StartsWith("join/", StringComparison.OrdinalIgnoreCase))
			token = token.Substring(5);

		_launcher.Visible = false;
		_settings.Visible = false;

		if (OS.GetName() == "iOS")
			poly_hide_polydroid_web();

		Node entry = FindEntry(GetTree().Root);
		MethodInfo method = entry?.GetType().GetMethod(
			"Entry",
			BindingFlags.Instance | BindingFlags.Public
		);

		ParameterInfo[] parameters = method?.GetParameters();
		if (entry == null || method == null || parameters == null || parameters.Length != 1)
			return;

		Type dataType = Nullable.GetUnderlyingType(parameters[0].ParameterType)
			?? parameters[0].ParameterType;

		object data = Activator.CreateInstance(dataType);
		dataType.GetField("Token")?.SetValue(data, token);
		dataType.GetProperty("Token")?.SetValue(data, token);
		method.Invoke(entry, new[] { data });
	}

	private static Node FindEntry(Node node)
	{
		if (node.GetType().FullName == "Polytoria.Client.ClientEntry")
			return node;

		foreach (Node child in node.GetChildren())
		{
			Node found = FindEntry(child);
			if (found != null)
				return found;
		}

		return null;
	}

	private void LayoutHud()
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

		_jumpBtn.Position = new Vector2(viewportSize.X - 96f, viewportSize.Y - 96f);
		_sprintBtn.Position = new Vector2(viewportSize.X - 180f, viewportSize.Y - 150f);
		_eBtn.Position = new Vector2(viewportSize.X - 96f, viewportSize.Y - 190f);
		_kbdBtn.Position = new Vector2(16f, 16f);
		_polyTopBtn.Position = new Vector2(68f, 16f);
		_fpsLabel.Position = new Vector2(176f, 22f);
		_hotbarRow.Position = new Vector2((viewportSize.X - 144f) * 0.5f, viewportSize.Y - 62f);

		for (int i = 0; i < _customBtns.Count; i++)
		{
			_customBtns[i].Position = new Vector2(
				viewportSize.X - 180f - (i * 58f),
				viewportSize.Y - 215f
			);
		}
	}

	public override void _Input(InputEvent e)
	{
		if (e is InputEventScreenTouch touch)
		{
			// Always process releases, even when a menu opened while a finger
			// was holding a control. This prevents stuck movement/jump actions.
			if (!touch.Pressed)
			{
				ReleaseTouch(touch.Index);
				return;
			}

			if (_launcher.Visible || _settings.Visible)
				return;

			Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

			if (_jumpIdx == -1 && touch.Position.DistanceTo(_jumpBtn.Position) <= 58f)
			{
				_jumpIdx = touch.Index;
				_jumpBtn.Pressed = true;
				Act("jump", true, 1f);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (touch.Position.DistanceTo(_sprintBtn.Position) <= 38f)
			{
				_sprint = !_sprint;
				_sprintBtn.Pressed = _sprint;
				Act("sprint", _sprint, 1f);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_eIdx == -1 && touch.Position.DistanceTo(_eBtn.Position) <= 34f)
			{
				_eIdx = touch.Index;
				_eBtn.Pressed = true;
				Act("interact", true, 1f);
				GetViewport().SetInputAsHandled();
				return;
			}

			for (int i = 0; i < _customBtns.Count; i++)
			{
				if (touch.Position.DistanceTo(_customBtns[i].Position) <= 34f)
				{
					_customBtns[i].Pressed = true;
					SendKeyTap(_customKeys[i]);
					GetViewport().SetInputAsHandled();
					return;
				}
			}

			// Let the HBoxContainer's buttons receive their own touch events.
			// Otherwise the same touch may be assigned to camera look/movement.
			if (_hotbarRow.GetGlobalRect().HasPoint(touch.Position))
				return;

			if (_moveIdx == -1 && touch.Position.X < viewportSize.X * 0.45f && touch.Position.Y > 64f)
			{
				_moveIdx = touch.Index;
				_stickCenter = touch.Position;
				_stickBase.Position = _stickKnob.Position = _stickCenter;
				_stickBase.Visible = _stickKnob.Visible = true;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_lookIdx == -1 && touch.Position.X >= viewportSize.X * 0.45f && touch.Position.Y > 64f)
			{
				_lookIdx = touch.Index;
				GetViewport().SetInputAsHandled();
			}
		}
		else if (e is InputEventScreenDrag drag)
		{
			if (_launcher.Visible || _settings.Visible)
				return;

			if (drag.Index == _moveIdx)
			{
				Vector2 delta = drag.Position - _stickCenter;
				_knobOffset = delta.Length() > MaxR ? delta.Normalized() * MaxR : delta;
				_stickKnob.Position = _stickCenter + _knobOffset;

				Vector2 normalized = _knobOffset / MaxR;
				Act("forward", normalized.Y < -Dead, -normalized.Y);
				Act("backward", normalized.Y > Dead, normalized.Y);
				Act("leftward", normalized.X < -Dead, -normalized.X);
				Act("rightward", normalized.X > Dead, normalized.X);

				GetViewport().SetInputAsHandled();
			}
			else if (drag.Index == _lookIdx)
			{
				Input.ParseInputEvent(new InputEventMouseMotion
				{
					Relative = drag.Relative * _camSens
				});
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void ReleaseTouch(int touchIndex)
	{
		if (touchIndex == _jumpIdx)
		{
			_jumpIdx = -1;
			_jumpBtn.Pressed = false;
			Act("jump", false, 0f);
		}

		if (touchIndex == _eIdx)
		{
			_eIdx = -1;
			_eBtn.Pressed = false;
			Act("interact", false, 0f);
		}

		if (touchIndex == _moveIdx)
		{
			_moveIdx = -1;
			_stickBase.Visible = false;
			_stickKnob.Visible = false;
			Act("forward", false, 0f);
			Act("backward", false, 0f);
			Act("leftward", false, 0f);
			Act("rightward", false, 0f);
		}

		if (touchIndex == _lookIdx)
			_lookIdx = -1;

		// Custom buttons send a tap immediately; this only resets their visual state.
		foreach (CircleBtn button in _customBtns)
			button.Pressed = false;
	}

	private static void SendKeyTap(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
		Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
	}

	private static void Act(string action, bool pressed, float strength)
	{
		if (action == "sprint")
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = pressed });

		if (action == "interact")
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, Pressed = pressed });

		if (!InputMap.HasAction(action))
			return;

		if (pressed)
			Input.ActionPress(action, Mathf.Clamp(strength, 0f, 1f));
		else if (Input.IsActionPressed(action))
			Input.ActionRelease(action);
	}

	private partial class CircleDraw : Control
	{
		private readonly float _radius;
		private readonly Color _fill;
		private readonly Color _stroke;

		public CircleDraw(float radius, Color fill, Color stroke)
		{
			_radius = radius;
			_fill = fill;
			_stroke = stroke;
		}

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, _radius, _fill);
			DrawArc(Vector2.Zero, _radius, 0, Mathf.Tau, 48, _stroke, 2.5f, true);
		}
	}

	private partial class CircleBtn : Control
	{
		private readonly float _radius;
		private readonly string _text;
		private readonly int _fontSize;
		private bool _pressed;

		public bool Pressed
		{
			get => _pressed;
			set
			{
				_pressed = value;
				QueueRedraw();
			}
		}

		public CircleBtn(float radius, string text, int fontSize)
		{
			_radius = radius;
			_text = text;
			_fontSize = fontSize;
		}

		public override void _Draw()
		{
			Color fill = _pressed
				? new Color(1, 1, 1, 0.42f)
				: new Color(0.15f, 0.16f, 0.20f, 0.55f);

			DrawCircle(Vector2.Zero, _radius, fill);
			DrawArc(Vector2.Zero, _radius, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.55f), 2.5f, true);

			Font font = ThemeDB.FallbackFont;
			Vector2 textSize = font.GetStringSize(
				_text,
				HorizontalAlignment.Center,
				-1,
				_fontSize
			);

			DrawString(
				font,
				new Vector2(-textSize.X * 0.5f, textSize.Y * 0.28f),
				_text,
				HorizontalAlignment.Center,
				-1,
				_fontSize,
				Colors.White
			);
		}
	}
}