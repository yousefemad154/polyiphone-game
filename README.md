polyiphone
An unofficial, native iOS & iPadOS port of the open-source Polytoria Godot C# client—built to run natively on ARM64 iPhones and iPads without emulators or desktop translation layers.


Features
	•	Polydroid-Inspired Launcher & Browser (WKWebView): Launches directly into a clean dark-mode launcher (Polytoria and Settings) with an integrated native iOS web browser to log in, browse Worlds, customize your Avatar, and launch games.
	•	Automatic polytoria:// Deep-Link Interception: Catches polytoria:// game launch links from the in-app browser and hands the session token straight into Polytoria's Godot C# client (ClientEntry).
	•	Customizable Mobile Touch HUD:
	•	Floating Left-Thumb Joystick: Appears wherever your left thumb touches on the left side of the screen.
	•	Right-Thumb Camera Look: Drag anywhere on the right side of the screen to rotate the camera smoothly.
	•	Action & Hotbar Controls: Dedicated circular Jump (↑), Sprint (Shift), Interact (E), bottom 1 / 2/ 3 item slots, and custom key buttons (F, Q, R, C).
	•	In-App Performance & Control Settings:
	•	Client Settings: Toggle between 720p and 1080p 3D render scaling, cap Max FPS (60 / 120 / 144 FPS), and toggle a live FPS stats overlay.
	•	Controls Settings: Adjust Camera sensitivity (0.5x – 5.0x) and spawn extra on-screen key buttons on demand.
	•	Native Apple Silicon / ARM64 Execution: Compiled directly for iOS (arm64) using .NET 10, Godot 4.7.2, and Xcode.



Building the .ipa via GitHub Actions
This repository includes an automated macOS cloud build workflow (.github/workflows/build-polyiphone.yml) that compiles the C# assembly, exports the iOS Xcode project, links native iOS WebKitbridges, and outputs an unsigned polyiphone.ipa:
	1.	Open the Actions tab in this repository.
	2.	Select Build polyiphone IPA from the left sidebar.
	3.	Click Run workflow > Run workflow.
	4.	Once the build completes, download polyiphone-ipa from the Artifacts section at the bottom of the workflow run.
	5.	Extract polyiphone.ipa and sign/install it on your iPhone or iPad using your preferred sideloading tool (such as SideStore, AltStore, Sideloadly, Feather, ESign, or KSign).



Project Structure
	•	PolyTouchControls.cs — Godot C# CanvasLayer implementing the dark-mode launcher, Client/Controls settings menu, polytoria:// token handoff, and multi-touch mobile HUD.
	•	.github/workflows/build-polyiphone.yml — GitHub Actions macOS build pipeline that prepares Godot 4.7.2 iOS C# export templates, compiles the .NET 10 project, injects Objective-C++ WKWebView / iOS SDK compatibility stubs (ios26_stubs.m), and packages polyiphone.ipa.
	•	Polytoria/ — Open-source Polytoria Godot client engine files.



License & Acknowledgments


	•	Built on top of the open-source Polytoria Client under the Mozilla Public License 2.0 (MPL-2.0).
	•	UI layout and mobile launcher workflow inspired by PolyDroid2.
	•	Disclaimer: polyiphone is an independent community port and is not officially affiliated with or endorsed by Polytoria.
