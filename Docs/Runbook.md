Shared env file can be placed at `Dev/.env` (or `/apps/.env` on VPS). Optional local overrides can be in `.env`/`.env.local` near the project.

cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube
dotnet run -- --print-auth-url



cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube

cat <<'EOF' | dotnet run
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"youtube.upload_video","arguments":{"file_path":"equalizer_fullscreen.mp4","title":"Equalizer Fullscreen","description":"Uploaded from MCP","privacy":"private"}}}
EOF

video id first Wmw6cXjHm9U

cat <<'EOF' | dotnet run
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"youtube.upload_thumbnail","arguments":{"video_id":"Wmw6cXjHm9U","image_path":"Gemini_Generated_Image_xf39hxxf39hxxf39.png"}}}
EOF

cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube

# Check if shell vars are overriding .env
printenv | grep '^YOUTUBE_'

# Clear overrides for this terminal session
unset YOUTUBE_CLIENT_SECRETS_PATH YOUTUBE_REFRESH_TOKEN YOUTUBE_REDIRECT_URI
dotnet run -- --exchange-code "4/0AfrIepCVFJ8ATSYJS3FUS0DE9ZkN4BUDbx4jDk3xVO8CHXE9XQrN7k65Tg9kbLpg_zgQFA"


//request credentials
dotnet run -- --print-auth-url
//refresh token 
dotnet run -- --exchange-code "4/0AfrIepDsCGTfZfDFREban17bjcx38pVUt6OVQ2v3W9SnjFmIhgt3xqAWuf7jDYnBsBmZ2w"




cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube

YOUTUBE_ALLOWED_ROOT="/Volumes/Data/Devs/Projects/test1" dotnet run << 'EOF'
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"youtube.upload_video","arguments":{"file_path":"/Volumes/Data/Devs/Projects/test1/20260303-181929-772eb192.mp4","title":"MCP Media Test Upload","description":"Uploaded manually from terminal","privacy":"private"}}}
EOF


cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube

YOUTUBE_ALLOWED_ROOT="/Volumes/Data/Devs/Projects/test1" dotnet run << 'EOF'
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"youtube.upload_thumbnail","arguments":{"video_id":"qWTI24i7UrY","image_path":"/Volumes/Data/Devs/Projects/test1/Gemini_Generated_Image_xf39hxxf39hxxf39.png"}}}
EOF


cd /Volumes/Data/Devs/Projects/SkydersLab/Dev/OnlineTeamTools.MCP.YouTube

cat <<'EOF' | dotnet run
{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"youtube.create_playlist","arguments":{"title":"Gym Mix March 2026","description":"Automated playlist from MCP","privacy":"unlisted"}}}
EOF

cat <<'EOF' | dotnet run
{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"youtube.add_videos_to_playlist","arguments":{"playlist_id":"PLxxxxxxxxxxxxxxxx","video_ids":["qWTI24i7UrY","Wmw6cXjHm9U"],"position":0}}}
EOF

cat <<'EOF' | dotnet run
{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"youtube.get_playlist","arguments":{"playlist_id":"PLxxxxxxxxxxxxxxxx"}}}
EOF
