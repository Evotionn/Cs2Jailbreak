using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Admin;
using MySqlConnector;

public class JailStats
{
    public JailStats()
    {
        for(int i = 0; i < 64; i++)
        {
            player_stats[i] = new PlayerStat();
        }
    }

    public void win(CCSPlayerController? player, LastRequest.LRType type)
    {
        var lr_player = player_stat_from_player(player);

        if(lr_player != null && type != LastRequest.LRType.NONE && player.is_valid())
        {
            int idx = (int)type;
            lr_player.win[idx] += 1;
            inc_db(player,type,true);
            Chat.announce(LastRequest.LR_PREFIX,$"{player.PlayerName} won {LastRequest.LR_NAME[idx]} win {lr_player.win[idx]} : loss {lr_player.loss[idx]}");
        }
    }

    public void loss(CCSPlayerController? player, LastRequest.LRType type)
    {
        var lr_player = player_stat_from_player(player);

        if(lr_player != null && type != LastRequest.LRType.NONE && player.is_valid())
        {
            int idx = (int)type;
            lr_player.loss[idx] += 1;
            inc_db(player,type,false);

            Chat.announce(LastRequest.LR_PREFIX,$"{player.PlayerName} lost {LastRequest.LR_NAME[idx]} win {lr_player.win[idx]} : loss {lr_player.loss[idx]}");
        }        
    }

    PlayerStat? player_stat_from_player(CCSPlayerController? player)
    {
        if(!player.is_valid())
        {
            return null;
        }

        return player_stats[player.Slot];        
    }



    void print_stats(CCSPlayerController? invoke, CCSPlayerController? player)
    {
        if(!invoke.is_valid())
        {
            return;
        }

        var lr_player = player_stat_from_player(player);

        if(lr_player != null && player.is_valid())
        {
            invoke.PrintToChat($"{LastRequest.LR_PREFIX} lr stats for {player.PlayerName}");

            for(int i = 0; i < LastRequest.LR_SIZE; i++)
            {
                invoke.PrintToChat($"{LastRequest.LR_PREFIX} {LastRequest.LR_NAME[i]} win {lr_player.win[i]} : loss {lr_player.loss[i]}");
            }
        }
    }

    public void lr_stats_cmd(CCSPlayerController? player, CommandInfo command)
    {
        // just do own player for now
        print_stats(player,player);
    }

    public void purge_player(CCSPlayerController? player)
    {
        var lr_player = player_stat_from_player(player);

        if(lr_player != null)
        {
            for(int i = 0; i < LastRequest.LR_SIZE; i++)
            {
                lr_player.win[i] = 0;
                lr_player.loss[i] = 0;
            }

            lr_player.cached = false;
        }
    }

    class PlayerStat
    {
        public int[] win = new int[LastRequest.LR_SIZE];
        public int[] loss = new int[LastRequest.LR_SIZE]; 
        public bool cached = false;
    }

    async void insert_player(String steam_id, String player_name)
    {
        var database = await connect_db();

        if(database == null)
        {
            return;
        }

        // insert new player
        using var insert_player = new MySqlCommand("INSERT IGNORE INTO stats (steamid,name) VALUES (@steam_id, @name)",database);
        insert_player.Parameters.AddWithValue("@steam_id",steam_id);
        insert_player.Parameters.AddWithValue("@name",player_name);

        try 
        {
            await insert_player.ExecuteNonQueryAsync();
        } 
        
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public void inc_db(CCSPlayerController? player,LastRequest.LRType type, bool win)
    {
        if(!player.is_valid() || type == LastRequest.LRType.NONE  || player.IsBot)
        {
            return;
        }

        String steam_id = new SteamID(player.SteamID).SteamId2;

        // make sure this doesn't block the main thread
        Task.Run(async () =>
        {
            await inc_db_async(steam_id,type,win);
        });
    }

    public async Task inc_db_async(String steam_id,LastRequest.LRType type, bool win)
    {
        var database = await connect_db();

        if(database == null)
        {
            return;
        }

        String name = LastRequest.LR_NAME[(int)type].Replace(" ","_");

        if(win)
        {
            name += "_win";
        }

        else
        {
            name += "_loss";
        }

        using var inc_stat = new MySqlCommand($"UPDATE stats SET {name} = {name} + 1 WHERE steamid = @steam_id",database);
        inc_stat.Parameters.AddWithValue("@steam_id",steam_id);

        try 
        {
            Console.WriteLine($"increment {steam_id} : {name} : {win}");
            await inc_stat.ExecuteNonQueryAsync();
        } 
        
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }


    void read_stats(ulong id, String steam_id, String player_name)
    {
         // repull player from steamid if they are still around
        CCSPlayerController? player = Utilities.GetPlayerFromSteamId(id);

        if(!player.is_valid())
        {
            return;
        }

        int slot = player.Slot;

        // allready cached we dont care
        if(player_stats[slot].cached)
        {
            return;
        }

        // make sure this doesn't block the main thread
        Task.Run(async () =>
        {
            await read_stats_async(steam_id,player_name,slot);
        });     
    }

    async Task read_stats_async(String steam_id, String player_name, int slot)
    {
        var database = await connect_db();

        if(database == null)
        {
            return;
        }

        // query steamid
        using var query_steam_id = new MySqlCommand("SELECT * FROM stats WHERE steamid = @steam_id",database);
        query_steam_id.Parameters.AddWithValue("@steam_id",steam_id);

        try
        {
            var reader = await query_steam_id.ExecuteReaderAsync();
            
            if(reader.Read())
            {
                //Console.WriteLine($"reading out lr stats {player.PlayerName}");

                for(int i = 0; i < LastRequest.LR_SIZE; i++)
                {
                    String name = LastRequest.LR_NAME[i].Replace(" ","_");

                    player_stats[slot].win[i] = (int)reader[name + "_win"];
                    player_stats[slot].loss[i] = (int)reader[name + "_loss"];
                }

                player_stats[slot].cached = true;
            }

            // failed to pull player stats
            // insert a new entry
            else
            {
                //Console.WriteLine("insert new entry");

                insert_player(steam_id,player_name);
            }

            reader.Close();
        }

        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    public void load_player(CCSPlayerController? player)
    {
        if(!player.is_valid())
        {
            return;
        }

        // attempt to cache player stats
        String name = player.PlayerName;
        String steam_id = new SteamID(player.SteamID).SteamId2;

        read_stats(player.SteamID,steam_id,name);
    }


    public void setup_db(MySqlConnection? database)
    {
        if(database == null)
        {
            Console.WriteLine("Could not open jb database");
            return;
        }

        // Make sure Table exists
        using var table_cmd = new MySqlCommand("CREATE TABLE IF NOT EXISTS stats (steamid varchar(64) PRIMARY KEY,name varchar(64))",database);
        table_cmd.ExecuteNonQuery();

        // Check table size to see if we have the right number of LR's
        // if we dont make the extra tables
        using var col_cmd = new MySqlCommand("SHOW COLUMNS FROM stats",database);
        var col_reader = col_cmd.ExecuteReader();

        int row_count = 0;
        while(col_reader.Read())
        {
            row_count++;
        }
        col_reader.Close();

        int fields = (LastRequest.LR_SIZE * 2) + 2;

        // NOTE: both win and lose i.e * 2 + steamid and name
        if(row_count != fields)
        {
            // add lr fields
            for(int i = 0; i < LastRequest.LR_SIZE; i++)
            {
                String name = LastRequest.LR_NAME[i].Replace(" ","_");

                try
                {
                    // NOTE: could use NOT Exists put old sql versions dont play nice
                    // ideally we would use an escaped statement but these strings aernt user controlled anyways
                    using var insert_table_win = new MySqlCommand($"ALTER TABLE stats ADD COLUMN {name + "_win"} int DEFAULT 0",database);
                    insert_table_win.ExecuteNonQuery();

                    using var insert_table_loss = new MySqlCommand($"ALTER TABLE stats ADD COLUMN {name + "_loss"} int DEFAULT 0",database);
                    insert_table_loss.ExecuteNonQuery();
                }

                catch {}
            }

            // add warden fields

        }

        Console.WriteLine("Setup jb stats");

    }

    public async Task<MySqlConnection?> connect_db()
    {
        // No credentials don't even try a connection
        if(config.username == "")
        {
            return null;
        }

        try
        {
            MySqlConnection? database = new MySqlConnection(
                $"Server={config.server};User ID={config.username};Password={config.password};Database={config.database};Port={config.port}");

            await database.OpenAsync();

            return database;
        }

        catch
        {
            //Console.WriteLine(ex.ToString());
            return null;
        }
    }

    public JailConfig config = new JailConfig();

    PlayerStat[] player_stats = new PlayerStat[64];
}