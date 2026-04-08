# SnapshotRunner.gd
# BoomHud Snapshot Runner for Godot 4.x
#
# Usage from CLI:
#   godot --headless --quit-after 60 --script res://SnapshotRunner.gd -- \
#     --scene "res://DebugOverlayView.tscn" \
#     --states "/path/to/states.json" \
#     --out "/path/to/output"
#
# Or via BoomHud CLI:
#   boomhud snapshot --manifest ui/boom-hud.compose.json --target godot

extends SceneTree

var scene_path: String = ""
var states_path: String = ""
var out_dir: String = ""
var verbose: bool = false

func _init() -> void:
	# Parse command line arguments
	var args := OS.get_cmdline_args()
	var i := 0
	while i < args.size():
		var arg := args[i]
		match arg:
			"--scene":
				i += 1
				if i < args.size():
					scene_path = args[i]
			"--states":
				i += 1
				if i < args.size():
					states_path = args[i]
			"--out":
				i += 1
				if i < args.size():
					out_dir = args[i]
			"--verbose":
				verbose = true
		i += 1
	
	if verbose:
		print("[SnapshotRunner] scene_path: ", scene_path)
		print("[SnapshotRunner] states_path: ", states_path)
		print("[SnapshotRunner] out_dir: ", out_dir)
	
	# Validate required args
	if scene_path.is_empty() or states_path.is_empty() or out_dir.is_empty():
		printerr("[SnapshotRunner] Missing required arguments")
		printerr("  --scene <path>   Scene to render (res:// or absolute)")
		printerr("  --states <path>  States JSON file")
		printerr("  --out <dir>      Output directory")
		quit(1)
		return
	
	# Defer main logic to allow scene tree to be ready
	call_deferred("_run")

func _run() -> void:
	if verbose:
		print("[SnapshotRunner] Starting render...")
	
	# Load states manifest
	var states := _load_states(states_path)
	if states.is_empty():
		printerr("[SnapshotRunner] Failed to load states from: ", states_path)
		quit(1)
		return
	
	# Get viewport config
	var viewport_config = states.get("viewport", {})
	var vp_width: int = viewport_config.get("width", 1280)
	var vp_height: int = viewport_config.get("height", 720)
	var vp_scale: float = viewport_config.get("scale", 1.0)
	
	# Get defaults
	var defaults = states.get("defaults", {})
	var default_wait_frames: int = defaults.get("waitFrames", 2)
	var default_background: String = defaults.get("background", "#1a1a2e")
	
	if verbose:
		print("[SnapshotRunner] Viewport: %dx%d @ %sx" % [vp_width, vp_height, vp_scale])
		print("[SnapshotRunner] Default waitFrames: %d, background: %s" % [default_wait_frames, default_background])
	
	# Create output directory
	DirAccess.make_dir_recursive_absolute(out_dir)
	
	# Load the scene
	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		printerr("[SnapshotRunner] Failed to load scene: ", scene_path)
		quit(1)
		return
	
	# Get states array
	var states_array: Array = states.get("states", [])
	if states_array.is_empty():
		printerr("[SnapshotRunner] No states defined in manifest")
		quit(1)
		return
	
	if verbose:
		print("[SnapshotRunner] Rendering %d states..." % states_array.size())
	
	# Create a SubViewport for rendering
	var viewport := SubViewport.new()
	viewport.size = Vector2i(int(vp_width * vp_scale), int(vp_height * vp_scale))
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	
	# Set background color if specified (null/empty means use default dark)
	if default_background.is_empty() or default_background == "null":
		default_background = "#1a1a2e"
	var bg_color := Color.html(default_background)
	var bg_rect := ColorRect.new()
	bg_rect.color = bg_color
	bg_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	viewport.add_child(bg_rect)
	
	root.add_child(viewport)
	
	# Process each state
	var index := 0
	for state_data in states_array:
		var state_name: String = state_data.get("name", "state_%d" % index)
		var vm_data = state_data.get("vm", {})
		
		# Per-state waitFrames override
		var wait_frames: int = state_data.get("waitFrames", default_wait_frames)
		
		if verbose:
			print("[SnapshotRunner] [%d/%d] Rendering: %s (waitFrames=%d)" % [index + 1, states_array.size(), state_name, wait_frames])
		
		# Instantiate scene fresh for each state
		var instance := packed_scene.instantiate()
		viewport.add_child(instance)
		
		# Apply VM state if the node has ApplyVmJson method
		if instance.has_method("ApplyVmJson"):
			var vm_json := JSON.stringify(vm_data)
			instance.call("ApplyVmJson", vm_json)
			if verbose:
				print("[SnapshotRunner]   Applied VM: ", vm_json.left(100), "...")
		
		# Wait for layout to settle (use waitFrames for determinism)
		for frame_i in range(wait_frames):
			await get_frame()
		
		# Capture viewport
		var image := viewport.get_texture().get_image()
		
		# Generate filename
		var filename := "%03d_%s.png" % [index, _sanitize_filename(state_name)]
		var output_path := out_dir.path_join(filename)
		
		# Save PNG
		var err := image.save_png(output_path)
		if err != OK:
			printerr("[SnapshotRunner] Failed to save PNG: ", output_path, " (error: ", err, ")")
		elif verbose:
			print("[SnapshotRunner]   Saved: ", output_path)
		
		# Clean up instance
		instance.queue_free()
		await get_frame()
		
		index += 1
	
	# Clean up
	viewport.queue_free()
	
	print("[SnapshotRunner] Done. Generated %d snapshots in %s" % [index, out_dir])
	quit(0)

func _load_states(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		printerr("[SnapshotRunner] Cannot open states file: ", path)
		return {}
	
	var json_text := file.get_as_text()
	file.close()
	
	var json := JSON.new()
	var err := json.parse(json_text)
	if err != OK:
		printerr("[SnapshotRunner] JSON parse error: ", json.get_error_message())
		return {}
	
	return json.data

func _sanitize_filename(name: String) -> String:
	# Remove invalid filename characters
	var invalid := ['/', '\\', ':', '*', '?', '"', '<', '>', '|']
	var result := name
	for c in invalid:
		result = result.replace(c, "_")
	return result

func get_frame() -> Signal:
	return process_frame
