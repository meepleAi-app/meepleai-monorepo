-- docker_name.lua
-- Reads the Docker container name from config.v2.json on the filesystem.
-- Called by the [FILTER] lua block in fluent-bit.conf.
--
-- Tag format for tail input with Path /var/lib/docker/containers/*/*.log:
--   docker.var.lib.docker.containers.<container-id>.<container-id>-json.log
-- We extract the container ID from the trailing segment before "-json.log".

local name_cache = {}

local function read_container_name(container_id)
    if name_cache[container_id] then
        return name_cache[container_id]
    end

    local config_path = "/var/lib/docker/containers/" .. container_id .. "/config.v2.json"
    local f = io.open(config_path, "r")
    if not f then
        name_cache[container_id] = container_id
        return container_id
    end

    local content = f:read("*all")
    f:close()

    -- Docker stores the name as "Name":"/meepleai-api" (with leading slash)
    local name = content:match('"Name":"/([^"]+)"')
    local result = name or container_id
    name_cache[container_id] = result
    return result
end

function add_container_name(tag, timestamp, record)
    -- Extract container ID: last segment before "-json.log" in the dot-encoded path
    -- e.g. docker.var.lib.docker.containers.abc123.abc123-json.log -> abc123
    local container_id = tag:match("%.([a-f0-9]+)-json%.log$")
    if container_id then
        record["container_name"] = read_container_name(container_id)
    end
    return 1, timestamp, record
end
