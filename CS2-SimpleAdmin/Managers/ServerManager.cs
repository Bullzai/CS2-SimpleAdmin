using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Cvars;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CS2_SimpleAdmin.Managers;

public class ServerManager
{
    private int _getIpTryCount = 0;

    public void LoadServerData()
    {
        CS2_SimpleAdmin.Instance.AddTimer(2.0f, () =>
        {
            if (CS2_SimpleAdmin.ServerLoaded || CS2_SimpleAdmin.ServerId != null || CS2_SimpleAdmin.Database == null) return;

            var ipAddress = ConVar.Find("ip")?.StringValue;

            if (string.IsNullOrEmpty(ipAddress) || ipAddress.StartsWith("0.0.0"))
            {
                ipAddress = Helper.GetServerIp();
            }

            if (string.IsNullOrEmpty(ipAddress) || ipAddress.StartsWith("0.0.0"))
            {
                if (_getIpTryCount < 12)
                {
                    _getIpTryCount++;
                    LoadServerData();
                    return;
                }
            }

            var address = $"{ipAddress}:{ConVar.Find("hostport")?.GetPrimitiveValue<int>()}";
            var hostname = ConVar.Find("hostname")!.StringValue;
            var rcon = ConVar.Find("rcon_password")!.StringValue;
            CS2_SimpleAdmin.IpAddress = address;

            Task.Run(async () =>
            {
                try
                {
                    await using var connection = await CS2_SimpleAdmin.Database.GetConnectionAsync();
                    var addressExists = await connection.ExecuteScalarAsync<bool>(
                        "SELECT COUNT(*) FROM sa_servers WHERE address = @address",
                        new { address });

                    if (!addressExists)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO sa_servers (address, hostname, rcon) VALUES (@address, @hostname, @rcon)",
                            new { address, hostname, rcon });
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                            "UPDATE `sa_servers` SET `hostname` = @hostname, rcon = @rcon, `id` = `id` WHERE `address` = @address",
                            new { address, rcon, hostname });
                    }

                    int? serverId = await connection.ExecuteScalarAsync<int>(
                        "SELECT `id` FROM `sa_servers` WHERE `address` = @address",
                        new { address });

                    CS2_SimpleAdmin.ServerId = serverId;

                    if (CS2_SimpleAdmin.ServerId != null)
                    {
                        await Server.NextFrameAsync(() => CS2_SimpleAdmin.Instance.ReloadAdmins(null));
                    }

                    CS2_SimpleAdmin.ServerLoaded = true;
                }
                catch (Exception ex)
                {
                    CS2_SimpleAdmin._logger?.LogCritical("Unable to create or get server_id: " + ex.Message);
                }

                if (CS2_SimpleAdmin.Instance.Config.EnableMetrics)
                {
                    var queryString = $"?address={address}&hostname={hostname}";
                    var client = CS2_SimpleAdmin.HttpClient;

                    try
                    {
                        await client.GetAsync($"https://api.daffyy.love/index.php{queryString}");
                    }
                    catch (HttpRequestException ex)
                    {
                        CS2_SimpleAdmin._logger?.LogWarning($"Unable to make metrics call: {ex.Message}");
                    }
                }
            });
        });
    }
}