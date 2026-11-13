using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

public partial class PolyTouchControls : CanvasLayer
{
	[DllImport("__Internal")]
	private static extern void poly_start_signin_with_polytoria();

	[DllImport("__Internal")]
	private static extern IntPtr poly_pop_captured_session();

	private Control _root;
	private Control _stickBase;
	private Control _stickKnob;
	private Control _jumpButton;

	private PanelContainer _homeOverlay;
	private Button _toggleHomeBtn;
	private LineEdit _searchInput;
	private LineEdit _tokenInput;
	private LineEdit _serverAddrInput;
	private Label _homeStatusLabel;
	private Label _userBadgeLabel;
	private Label _pageLabel;
	private GridContainer _cardsGrid;

	private HttpRequest _placesRequest;
	private HttpRequest _friendsRequest;
	private HttpRequest _userRequest;
	private HttpRequest _joinRequest;

	private int _moveTouchIndex = -1;
	private int _jumpTouchIndex = -1;
	private Vector2 _stickCenter = Vector2.Zero;
	private Vector2 _knobOffset = Vector2.Zero;
	private double _authPollTimer = 0.0;

	private string _activeTab = "worlds";
	private string _sortMode = "playing";
	private int _currentPage = 1;
	private string _userId = "";
	private string _username = "yousefgalaxy";

	private const float MaxRadius = 64.0f;
	private const float Deadzone = 0.18f;
	private const string SavePath = "user://polyiphone_auth.cfg";

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

		BuildAppShell();
		LoadSavedSession();

		PositionControls();
		GetViewport().SizeChanged += PositionControls;

		FetchUserProfile();
		FetchWorldsPage(1);
	}

	public override void _Process(double delta)
	{
		_authPollTimer += delta;
		if (_authPollTimer < 0.4) return;
		_authPollTimer = 0.0;

		if (OS.GetName() == "iOS")
		{
			try
			{
				IntPtr ptr = poly_pop_captured_session();
				if (ptr != IntPtr.Zero)
				{
					string captured = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
					Marshal.FreeHGlobal(ptr);
					if (!string.IsNullOrEmpty(captured))
					{
						_tokenInput.Text = captured;
						SaveSession();
						_homeStatusLabel.Text = "Signed in with Polytoria! Loading profile...";
						FetchUserProfile();
					}
				}
			}
			catch { }
		}
	}

	private void BuildAppShell()
	{
		_placesRequest = new HttpRequest();
		_placesRequest.RequestCompleted += OnPlacesCompleted;
		AddChild(_placesRequest);

		_friendsRequest = new HttpRequest();
		_friendsRequest.RequestCompleted += OnFriendsCompleted;
		AddChild(_friendsRequest);

		_userRequest = new HttpRequest();
		_userRequest.RequestCompleted += OnUserCompleted;
		AddChild(_userRequest);

		_joinRequest = new HttpRequest();
		_joinRequest.RequestCompleted += OnJoinCompleted;
		AddChild(_joinRequest);

		_toggleHomeBtn = new Button();
		_toggleHomeBtn.Text = "Home";
		_toggleHomeBtn.CustomMinimumSize = new Vector2(84f, 38f);
		_toggleHomeBtn.AddThemeFontSizeOverride("font_size", 15);
		_toggleHomeBtn.Visible = false;
		_toggleHomeBtn.Pressed += () => SetHomeVisible(true);
		_root.AddChild(_toggleHomeBtn);

		_homeOverlay = new PanelContainer();
		_homeOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_homeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;

		StyleBoxFlat bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = new Color(0.075f, 0.086f, 0.11f, 1.0f);
		_homeOverlay.AddThemeStyleboxOverride("panel", bgStyle);
		_root.AddChild(_homeOverlay);

		VBoxContainer mainCol = new VBoxContainer();
		mainCol.AddThemeConstantOverride("separation", 10);
		_homeOverlay.AddChild(mainCol);

		PanelContainer navBar = new PanelContainer();
		StyleBoxFlat navStyle = new StyleBoxFlat();
		navStyle.BgColor = new Color(0.105f, 0.122f, 0.157f, 1.0f);
		navStyle.ContentMarginLeft = 18;
		navStyle.ContentMarginRight = 18;
		navStyle.ContentMarginTop = 10;
		navStyle.ContentMarginBottom = 10;
		navBar.AddThemeStyleboxOverride("panel", navStyle);
		mainCol.AddChild(navBar);

		HBoxContainer navRow = new HBoxContainer();
		navRow.AddThemeConstantOverride("separation", 10);
		navBar.AddChild(navRow);

		Label brandLabel = new Label();
		brandLabel.Text = "POLYIPHONE";
		brandLabel.AddThemeFontSizeOverride("font_size", 21);
		brandLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.35f, 0.35f, 1.0f));
		navRow.AddChild(brandLabel);

		Button worldsTabBtn = new Button();
		worldsTabBtn.Text = "Worlds";
		worldsTabBtn.Pressed += () => SwitchTab("worlds");
		navRow.AddChild(worldsTabBtn);

		Button friendsTabBtn = new Button();
		friendsTabBtn.Text = "Friends";
		friendsTabBtn.Pressed += () => SwitchTab("friends");
		navRow.AddChild(friendsTabBtn);

		_searchInput = new LineEdit();
		_searchInput.PlaceholderText = "Search all Polytoria worlds...";
		_searchInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_searchInput.TextSubmitted += (_) => { _currentPage = 1; FetchWorldsPage(1); };
		navRow.AddChild(_searchInput);

		Button searchBtn = new Button();
		searchBtn.Text = "Search";
		searchBtn.Pressed += () => { _currentPage = 1; FetchWorldsPage(1); };
		navRow.AddChild(searchBtn);

		_userBadgeLabel = new Label();
		_userBadgeLabel.Text = "@" + _username;
		_userBadgeLabel.AddThemeFontSizeOverride("font_size", 14);
		_userBadgeLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.65f, 1.0f));
		navRow.AddChild(_userBadgeLabel);

		Button hideBtn = new Button();
		hideBtn.Text = "Hide";
		hideBtn.Pressed += () => SetHomeVisible(false);
		navRow.AddChild(hideBtn);

		MarginContainer contentMargin = new MarginContainer();
		contentMargin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		contentMargin.AddThemeConstantOverride("margin_left", 20);
		contentMargin.AddThemeConstantOverride("margin_right", 20);
		contentMargin.AddThemeConstantOverride("margin_bottom", 14);
		mainCol.AddChild(contentMargin);

		VBoxContainer bodyCol = new VBoxContainer();
		bodyCol.AddThemeConstantOverride("separation", 10);
		contentMargin.AddChild(bodyCol);

		PanelContainer authCard = new PanelContainer();
		StyleBoxFlat cardBg = new StyleBoxFlat();
		cardBg.BgColor = new Color(0.118f, 0.137f, 0.18f, 1.0f);
		cardBg.CornerRadiusTopLeft = 8;
		cardBg.CornerRadiusTopRight = 8;
		cardBg.CornerRadiusBottomLeft = 8;
		cardBg.CornerRadiusBottomRight = 8;
		cardBg.ContentMarginLeft = 14;
		cardBg.ContentMarginRight = 14;
		cardBg.ContentMarginTop = 10;
		cardBg.ContentMarginBottom = 10;
		authCard.AddThemeStyleboxOverride("panel", cardBg);
		bodyCol.AddChild(authCard);

		VBoxContainer authCol = new VBoxContainer();
		authCol.AddThemeConstantOverride("separation", 8);
		authCard.AddChild(authCol);

		HBoxContainer serverRow = new HBoxContainer();
		serverRow.AddThemeConstantOverride("separation", 8);
		authCol.AddChild(serverRow);

		Label srvLbl = new Label();
		srvLbl.Text = "Private Server:";
		srvLbl.AddThemeFontSizeOverride("font_size", 14);
		srvLbl.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 1.0f, 1.0f));
		serverRow.AddChild(srvLbl);

		_serverAddrInput = new LineEdit();
		_serverAddrInput.PlaceholderText = "Enter server IP:Port (e.g. 147.185.221.20:24221)";
		_serverAddrInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		serverRow.AddChild(_serverAddrInput);

		Button joinSelfHostBtn = new Button();
		joinSelfHostBtn.Text = "Join Private Server";
		StyleBoxFlat srvBtnStyle = new StyleBoxFlat();
		srvBtnStyle.BgColor = new Color(0.15f, 0.52f, 0.88f, 1.0f);
		srvBtnStyle.CornerRadiusTopLeft = 6;
		srvBtnStyle.CornerRadiusTopRight = 6;
		srvBtnStyle.CornerRadiusBottomLeft = 6;
		srvBtnStyle.CornerRadiusBottomRight = 6;
		srvBtnStyle.ContentMarginLeft = 14;
		srvBtnStyle.ContentMarginRight = 14;
		joinSelfHostBtn.AddThemeStyleboxOverride("normal", srvBtnStyle);
		joinSelfHostBtn.Pressed += OnJoinPrivateServerPressed;
		serverRow.AddChild(joinSelfHostBtn);

		HBoxContainer tokenRow = new HBoxContainer();
		tokenRow.AddThemeConstantOverride("separation", 8);
		authCol.AddChild(tokenRow);

		Button polySignInBtn = new Button();
		polySignInBtn.Text = "Sign in with Polytoria";
		StyleBoxFlat signInStyle = new StyleBoxFlat();
		signInStyle.BgColor = new Color(0.88f, 0.28f, 0.28f, 1.0f);
		signInStyle.CornerRadiusTopLeft = 6;
		signInStyle.CornerRadiusTopRight = 6;
		signInStyle.CornerRadiusBottomLeft = 6;
		signInStyle.CornerRadiusBottomRight = 6;
		signInStyle.ContentMarginLeft = 12;
		signInStyle.ContentMarginRight = 12;
		polySignInBtn.AddThemeStyleboxOverride("normal", signInStyle);
		polySignInBtn.Pressed += () =>
		{
			_homeStatusLabel.Text = "Sign in on Polytoria, then tap Done in the top-left to return!";
			if (OS.GetName() == "iOS")
			{
				poly_start_signin_with_polytoria();
			}
			else
			{
				OS.ShellOpen("https://polytoria.com");
			}
		};
		tokenRow.AddChild(polySignInBtn);

		_tokenInput = new LineEdit();
		_tokenInput.PlaceholderText = "Session token OR polytoria:// link...";
		_tokenInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		tokenRow.AddChild(_tokenInput);

		Button saveTokenBtn = new Button();
		saveTokenBtn.Text = "Save";
		saveTokenBtn.Pressed += OnSaveAndSignInPressed;
		tokenRow.AddChild(saveTokenBtn);

Button joinLinkBtn = new Button();
joinLinkBtn.Text = "Join Link";
joinLinkBtn.Pressed += OnDirectJoinPressed;
tokenRow.AddChild(joinLinkBtn);
HBoxContainer filterRow = new HBoxContainer();
filterRow.AddThemeConstantOverride("separation", 8);
authCol.AddChild(filterRow);
Button sortPlayingBtn = new Button();
sortPlayingBtn.Text = "Popular";
sortPlayingBtn.Pressed += () => { _sortMode = "playing"; FetchWorldsPage(1); };
filterRow.AddChild(sortPlayingBtn);
Button sortVisitsBtn = new Button();
sortVisitsBtn.Text = "Top Visits";
sortVisitsBtn.Pressed += () => { _sortMode = "visits"; FetchWorldsPage(1); };
filterRow.AddChild(sortVisitsBtn);
Button sortNewestBtn = new Button();
sortNewestBtn.Text = "Newest";
sortNewestBtn.Pressed += () => { _sortMode = "createdAt"; FetchWorldsPage(1); };
filterRow.AddChild(sortNewestBtn);
_homeStatusLabel = new Label();
_homeStatusLabel.Text = "Enter your Private Server IP:Port above to play!";
_homeStatusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
_homeStatusLabel.AddThemeFontSizeOverride("font_size", 13);
_homeStatusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.82f, 0.92f, 1.0f));
filterRow.AddChild(_homeStatusLabel);
Button prevPageBtn = new Button();
prevPageBtn.Text = "< Prev";
prevPageBtn.Pressed += () => { if (_currentPage > 1) FetchWorldsPage(_currentPage - 1); };
filterRow.AddChild(prevPageBtn);
_pageLabel = new Label();
_pageLabel.Text = "Page 1";
filterRow.AddChild(_pageLabel);
Button nextPageBtn = new Button();
nextPageBtn.Text = "Next >";
nextPageBtn.Pressed += () => FetchWorldsPage(_currentPage + 1);
filterRow.AddChild(nextPageBtn);
ScrollContainer scroll = new ScrollContainer();
scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
bodyCol.AddChild(scroll);
_cardsGrid = new GridContainer();
_cardsGrid.Columns = 3;
_cardsGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
_cardsGrid.AddThemeConstantOverride("h_separation", 12);
_cardsGrid.AddThemeConstantOverride("v_separation", 12);
scroll.AddChild(_cardsGrid);
}
private void OnJoinPrivateServerPressed()
{
string addrRaw = _serverAddrInput.Text.Trim();
if (string.IsNullOrEmpty(addrRaw))
{
_homeStatusLabel.Text = "Enter your server IP:Port first (for example: 147.185.221.20:24221).";
return;
}
SaveSession();
string host = addrRaw;
int port = 24221;
int colonIdx = addrRaw.LastIndexOf(':');
if (colonIdx > 0 && colonIdx < addrRaw.Length - 1)
{
host = addrRaw.Substring(0, colonIdx).Trim();
int.TryParse(addrRaw.Substring(colonIdx + 1).Trim(), out port);
}
int uid = 1144;
int.TryParse(_userId, out uid);
if (uid <= 0) uid = 1144;
_homeStatusLabel.Text = "Connecting to " + host + ":" + port + "...";
SetHomeVisible(false);
Node entryNode = FindClientEntryNode(GetTree().Root);
if (entryNode != null)
{
InvokeSelfHostEntry(entryNode, host, port, uid);
}
}
private static void InvokeSelfHostEntry(Node clientEntry, string host, int port, int userId)
{
Type entryType = clientEntry.GetType();
MethodInfo entryMethod = entryType.GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
if (entryMethod == null) return;
ParameterInfo[] parameters = entryMethod.GetParameters();
if (parameters.Length == 1)
{
Type paramType = parameters[0].ParameterType;
Type dataType = Nullable.GetUnderlyingType(paramType) ?? paramType;
object dataObj = Activator.CreateInstance(dataType);
SetMemberValue(dataType, dataObj, "TestIsServer", false);
SetMemberValue(dataType, dataObj, "ConnectAddress", host);
SetMemberValue(dataType, dataObj, "ConnectPort", port);
SetMemberValue(dataType, dataObj, "TestUserID", userId);
entryMethod.Invoke(clientEntry, new object[] { dataObj });
}
}
private static void SetMemberValue(Type t, object target, string name, object value)
{
FieldInfo fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
if (fi != null)
{
fi.SetValue(target, value);
return;
}
PropertyInfo pi = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
if (pi != null && pi.CanWrite)
{
pi.SetValue(target, value);
}
}
private void SwitchTab(string tab)
{
_activeTab = tab;
if (tab == "worlds")
{
FetchWorldsPage(_currentPage);
}
else if (tab == "friends")
{
FetchFriendsList();
}
}
private void SetHomeVisible(bool visible)
{
_homeOverlay.Visible = visible;
_toggleHomeBtn.Visible = !visible;
}
private void LoadSavedSession()
{
ConfigFile cfg = new ConfigFile();
if (cfg.Load(SavePath) == Error.Ok)
{
_tokenInput.Text = (string)cfg.GetValue("auth", "token", "");
_serverAddrInput.Text = (string)cfg.GetValue("auth", "server_addr", "");
_username = (string)cfg.GetValue("auth", "username", "yousefgalaxy");
_userId = (string)cfg.GetValue("auth", "userid", "");
_userBadgeLabel.Text = "@" + _username;
}
}
private void SaveSession()
{
ConfigFile cfg = new ConfigFile();
cfg.SetValue("auth", "token", _tokenInput.Text.Trim());
cfg.SetValue("auth", "server_addr", _serverAddrInput.Text.Trim());
cfg.SetValue("auth", "username", _username);
cfg.SetValue("auth", "userid", _userId);
cfg.Save(SavePath);
}
private void OnSaveAndSignInPressed()
{
SaveSession();
_homeStatusLabel.Text = "Saved! Verifying account with Polytoria...";
FetchUserProfile();
}
private void FetchUserProfile()
{
string auth = _tokenInput.Text.Trim();
List headers = new List { "User-Agent: polyiphone/1.0", "Accept: application/json" };
if (!string.IsNullOrEmpty(auth) && !auth.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
{
headers.Add("Cookie: polytoria_session=" + auth);
headers.Add("Authorization: Bearer " + auth);
}
_userRequest.Request("polytoria.com" + _username.UriEncode(), headers.ToArray());
}
private void OnUserCompleted(long result, long responseCode, string[] headers, byte[] body)
{
if (responseCode != 200 || body == null) return;
Variant parsed = Json.ParseString(Encoding.UTF8.GetString(body));
if (parsed.VariantType == Variant.Type.Dictionary)
{
Godot.Collections.Dictionary dict = parsed.AsGodotDictionary();
if (dict.ContainsKey("id")) _userId = dict["id"].ToString();
if (dict.ContainsKey("username")) _username = dict["username"].ToString();
_userBadgeLabel.Text = "@" + _username;
SaveSession();
if (_activeTab == "friends") FetchFriendsList();
}
}
private void FetchWorldsPage(int page)
{
_activeTab = "worlds";
_currentPage = page;
_pageLabel.Text = "Page " + _currentPage;
string q = _searchInput.Text.Trim();
string url = "polytoria.com" + _currentPage + "&sort=" + _sortMode + "&order=desc";
if (!string.IsNullOrEmpty(q))
{
url += "&search=" + q.UriEncode();
}
string[] headers = { "User-Agent: polyiphone/1.0", "Accept: application/json" };
_placesRequest.Request(url, headers);
}
private void OnPlacesCompleted(long result, long responseCode, string[] headers, byte[] body)
{
ClearCards();
if (responseCode != 200 || body == null) return;
Variant parsed = Json.ParseString(Encoding.UTF8.GetString(body));
if (parsed.VariantType != Variant.Type.Dictionary) return;
Godot.Collections.Dictionary rootDict = parsed.AsGodotDictionary();
Godot.Collections.Array arr = rootDict.ContainsKey("places") ? rootDict["places"].AsGodotArray() : null;
if (arr == null) return;
foreach (Variant item in arr)
{
if (item.VariantType != Variant.Type.Dictionary) continue;
Godot.Collections.Dictionary p = item.AsGodotDictionary();
string id = p.ContainsKey("id") ? p["id"].ToString() : "";
string name = p.ContainsKey("name") ? p["name"].ToString() : "Untitled World";
string playing = p.ContainsKey("playing") ? p["playing"].ToString() : "0";
string visits = p.ContainsKey("visits") ? p["visits"].ToString() : "0";
string creator = "Creator";
if (p.ContainsKey("creator") && p["creator"].VariantType == Variant.Type.Dictionary)
{
Godot.Collections.Dictionary c = p["creator"].AsGodotDictionary();
if (c.ContainsKey("name")) creator = c["name"].ToString();
}


AddCard(name, "By " + creator + " • " + playing + " playing • " + visits + " visits", "Play", new Color(0.16f, 0.65f, 0.32f, 1.0f), () => OnPlacePlayPressed(id, name));
}
}
private void FetchFriendsList()
{
ClearCards();
if (string.IsNullOrEmpty(_userId))
{
FetchUserProfile();
return;
}
string[] headers = { "User-Agent: polyiphone/1.0", "Accept: application/json" };
_friendsRequest.Request("polytoria.com" + _userId + "/friends?limit=30", headers);
}
private void OnFriendsCompleted(long result, long responseCode, string[] headers, byte[] body)
{
ClearCards();
if (responseCode != 200 || body == null) return;
Variant parsed = Json.ParseString(Encoding.UTF8.GetString(body));
if (parsed.VariantType != Variant.Type.Dictionary) return;
Godot.Collections.Dictionary rootDict = parsed.AsGodotDictionary();
Godot.Collections.Array arr = rootDict.ContainsKey("friends") ? rootDict["friends"].AsGodotArray() : (rootDict.ContainsKey("data") ? rootDict["data"].AsGodotArray() : null);
if (arr == null) return;
foreach (Variant item in arr)
{
if (item.VariantType != Variant.Type.Dictionary) continue;
Godot.Collections.Dictionary f = item.AsGodotDictionary();
if (f.ContainsKey("user") && f["user"].VariantType == Variant.Type.Dictionary)
{
f = f["user"].AsGodotDictionary();
}
string fname = f.ContainsKey("username") ? f["username"].ToString() : "Friend";
bool online = f.ContainsKey("online") && f["online"].AsBool();
string playingPlaceId = "";
if (f.ContainsKey("playing") && f["playing"].VariantType == Variant.Type.Dictionary)
{
Godot.Collections.Dictionary pl = f["playing"].AsGodotDictionary();
if (pl.ContainsKey("id")) playingPlaceId = pl["id"].ToString();
}
string statusText = !string.IsNullOrEmpty(playingPlaceId) ? "In Game (ID " + playingPlaceId + ")" : (online ? "Online" : "Offline");
string btnLabel = !string.IsNullOrEmpty(playingPlaceId) ? "Join Friend" : "Profile";
Color btnColor = !string.IsNullOrEmpty(playingPlaceId) ? new Color(0.18f, 0.55f, 0.90f, 1.0f) : new Color(0.25f, 0.28f, 0.35f, 1.0f);
AddCard("@" + fname, statusText, btnLabel, btnColor, () =>
{
if (!string.IsNullOrEmpty(playingPlaceId))
{
OnPlacePlayPressed(playingPlaceId, fname + "'s game");
}
});
}
}
private void ClearCards()
{
foreach (Node child in _cardsGrid.GetChildren())
{
child.QueueFree();
}
}
private void AddCard(string title, string subtitle, string buttonText, Color buttonColor, Action onClick)
{
PanelContainer card = new PanelContainer();
card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
StyleBoxFlat cardStyle = new StyleBoxFlat();
cardStyle.BgColor = new Color(0.118f, 0.137f, 0.18f, 1.0f);
cardStyle.CornerRadiusTopLeft = 8;
cardStyle.CornerRadiusTopRight = 8;
cardStyle.CornerRadiusBottomLeft = 8;
cardStyle.CornerRadiusBottomRight = 8;
cardStyle.ContentMarginLeft = 12;
cardStyle.ContentMarginRight = 12;
cardStyle.ContentMarginTop = 10;
cardStyle.ContentMarginBottom = 10;
card.AddThemeStyleboxOverride("panel", cardStyle);
VBoxContainer col = new VBoxContainer();
col.AddThemeConstantOverride("separation", 6);
card.AddChild(col);
Label titleLbl = new Label();
titleLbl.Text = title;
titleLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
titleLbl.AddThemeFontSizeOverride("font_size", 15);
titleLbl.AddThemeColorOverride("font_color", Colors.White);
col.AddChild(titleLbl);
Label subLbl = new Label();
subLbl.Text = subtitle;
subLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
subLbl.AddThemeFontSizeOverride("font_size", 12);
subLbl.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.82f, 1.0f));
col.AddChild(subLbl);
Button btn = new Button();
btn.Text = buttonText;
btn.CustomMinimumSize = new Vector2(0f, 34f);
StyleBoxFlat btnStyle = new StyleBoxFlat();
btnStyle.BgColor = buttonColor;
btnStyle.CornerRadiusTopLeft = 6;
btnStyle.CornerRadiusTopRight = 6;
btnStyle.CornerRadiusBottomLeft = 6;
btnStyle.CornerRadiusBottomRight = 6;
btn.AddThemeStyleboxOverride("normal", btnStyle);
btn.Pressed += onClick;
col.AddChild(btn);
_cardsGrid.AddChild(card);
}
private void OnDirectJoinPressed()
{
string raw = _tokenInput.Text.Trim();
if (string.IsNullOrEmpty(raw))
{
raw = DisplayServer.ClipboardGet()?.Trim() ?? string.Empty;
if (!string.IsNullOrEmpty(raw))
{
_tokenInput.Text = raw;
SaveSession();
}
}
if (string.IsNullOrEmpty(raw))
{
_homeStatusLabel.Text = "Paste a polytoria:// join link into the box first.";
return;
}
ConnectToClientEntry(raw);
}
private void OnPlacePlayPressed(string placeId, string placeName)
{
string auth = _tokenInput.Text.Trim();
if (auth.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
{
ConnectToClientEntry(auth);
return;
}
if (string.IsNullOrEmpty(auth))
{
_homeStatusLabel.Text = "Official public servers require the official desktop client. Enter your Private Server IP:Port at the top to play!";
return;
}
_homeStatusLabel.Text = "Connecting to " + placeName + "...";
string[] headers =
{
"User-Agent: polyiphone/1.0",
"Accept: application/json",
"Content-Type: application/json",
"Authorization: Bearer " + auth,
"Cookie: polytoria_session=" + auth
};
string payload = "{"placeID":" + placeId + "}";
_joinRequest.Request("polytoria.com", headers, HttpClient.Method.Post, payload);
}
private void OnJoinCompleted(long result, long responseCode, string[] headers, byte[] body)
{
string text = body != null ? Encoding.UTF8.GetString(body) : "";
int idx = text.IndexOf("polytoria://", StringComparison.OrdinalIgnoreCase);
if (idx >= 0)
{
string sub = text.Substring(idx);
int end = sub.IndexOfAny(new[] { '"', ''', ' ', '\n', '\r' });
string uri = end > 0 ? sub.Substring(0, end) : sub;
ConnectToClientEntry(uri);
return;
}
Variant parsed = Json.ParseString(text);
if (parsed.VariantType == Variant.Type.Dictionary)
{
Godot.Collections.Dictionary dict = parsed.AsGodotDictionary();
if (dict.ContainsKey("token"))
{
ConnectToClientEntry(dict["token"].ToString());
return;
}
}
_homeStatusLabel.Text = "Official public servers block custom clients (HTTP " + responseCode + "). Use Private Server IP:Port at the top!";
}
private void ConnectToClientEntry(string rawLinkOrToken)
{
string cleanToken = rawLinkOrToken.Trim();
if (cleanToken.StartsWith("polytoria://", StringComparison.OrdinalIgnoreCase))
{
cleanToken = cleanToken.Substring("polytoria://".Length).TrimStart('/');
if (cleanToken.StartsWith("join/", StringComparison.OrdinalIgnoreCase))
{
cleanToken = cleanToken.Substring("join/".Length);
}
}
SetHomeVisible(false);
Node entryNode = FindClientEntryNode(GetTree().Root);
if (entryNode != null)
{
InvokeClientEntry(entryNode, cleanToken);
}
}
private static Node FindClientEntryNode(Node node)
{
if (node.GetType().FullName == "Polytoria.Client.ClientEntry")
{
return node;
}
foreach (Node child in node.GetChildren())
{
Node found = FindClientEntryNode(child);
if (found != null) return found;
}
return null;
}
private static void InvokeClientEntry(Node clientEntry, string token)
{
Type entryType = clientEntry.GetType();
MethodInfo entryMethod = entryType.GetMethod("Entry", BindingFlags.Instance | BindingFlags.Public);
if (entryMethod == null) return;
ParameterInfo[] parameters = entryMethod.GetParameters();
if (parameters.Length == 1)
{
Type paramType = parameters[0].ParameterType;
Type dataType = Nullable.GetUnderlyingType(paramType) ?? paramType;
object dataObj = Activator.CreateInstance(dataType);
SetMemberValue(dataType, dataObj, "Token", token);
entryMethod.Invoke(clientEntry, new object[] { dataObj });
}
}
private void PositionControls()
{
Vector2 viewSize = GetViewport().GetVisibleRect().Size;
_jumpButton.Position = new Vector2(viewSize.X - 110.0f, viewSize.Y - 110.0f);
_toggleHomeBtn.Position = new Vector2(18.0f, 18.0f);
_cardsGrid.Columns = viewSize.X >= 900.0f ? 3 : 2;
}
public override void _Input(InputEvent @event)
{
if (_homeOverlay != null && _homeOverlay.Visible)
{
return;
}
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

