# OnlineTeamTools.MCP.YouTube

`OnlineTeamTools.MCP.YouTube` is a .NET 8 MCP server that communicates over `stdin/stdout` using JSON-RPC 2.0.

- Long-lived process
- One JSON-RPC request per input line
- One JSON-RPC response per output line
- Methods supported: `tools/list`, `tools/call`
- OAuth secrets/tokens stay inside this MCP server

## Configuration

Set via environment variables (preferred) or `appsettings.json` (`YouTube:*` keys).

Required for tool execution:

- `YOUTUBE_CLIENT_SECRETS_PATH` path to Google OAuth `client_secret.json`
- `YOUTUBE_REFRESH_TOKEN` OAuth refresh token

Optional:

- `YOUTUBE_APPLICATION_NAME` default: `MCP.Youtube`
- `YOUTUBE_ALLOWED_ROOT` default: `/Volumes/Data/Devs/Projects` (fallback: current directory)
- `YOUTUBE_MAX_FILE_MB` default: `2048`
- `YOUTUBE_DEFAULT_PRIVACY` default: `private` (`public` is never used unless explicitly passed per call)
- `YOUTUBE_CONCURRENCY` default: `1`
- `YOUTUBE_ALLOWED_VIDEO_EXTENSIONS` default: `.mp4,.mov`
- `YOUTUBE_ALLOWED_IMAGE_EXTENSIONS` default: `.jpg,.jpeg,.png`
- `YOUTUBE_TOOL_TIMEOUT_SECONDS` default: `1800`
- `YOUTUBE_REDIRECT_URI` default: `http://localhost:53682/callback`

## OAuth Helper Modes

Get one-time authorization URL:

```bash
dotnet run --project OnlineTeamTools.MCP.YouTube -- --print-auth-url
```

Exchange auth code for refresh token:

```bash
dotnet run --project OnlineTeamTools.MCP.YouTube -- --exchange-code <CODE>
```

## Run Locally

```bash
dotnet run --project OnlineTeamTools.MCP.YouTube
```

Then send JSON-RPC lines over stdin.

## Example JSON-RPC Calls

`tools/list`

```json
{"jsonrpc":"2.0","id":1,"method":"tools/list"}
```

`tools/call` create async upload job

```json
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"youtube.create_upload_job","arguments":{"file_path":"uploads/demo.mp4","title":"Demo","description":"My upload","privacy":"private"}}}
```

`tools/call` get job

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"youtube.get_job","arguments":{"job_id":"<job_id>"}}}
```

`tools/call` upload thumbnail

```json
{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"youtube.upload_thumbnail","arguments":{"video_id":"<video_id>","image_path":"thumbs/demo.jpg"}}}
```

`tools/call` update metadata

```json
{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"youtube.update_metadata","arguments":{"video_id":"<video_id>","title":"Updated title","privacy":"unlisted"}}}
```

`tools/call` get video

```json
{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"youtube.get_video","arguments":{"video_id":"<video_id>"}}}
```

## Safety Defaults

- Paths are restricted to `YOUTUBE_ALLOWED_ROOT`
- Symlinked paths are rejected
- File existence/size/extension checks are enforced
- Default privacy is private
- Concurrent uploads are limited by `YOUTUBE_CONCURRENCY`
- Logs include request id/tool/job id and never include OAuth secrets

## Publish

```bash
dotnet publish -c Release -r osx-arm64 --self-contained false
```

## Manual Acceptance Flow

1. Run server.
2. Call `tools/list`.
3. Call `youtube.create_upload_job` with a test MP4 under allowed root.
4. Poll `youtube.get_job` until completed.
5. Call `youtube.upload_thumbnail`.
6. Call `youtube.update_metadata`.
7. Call `youtube.get_video`.
