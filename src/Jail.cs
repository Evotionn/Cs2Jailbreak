﻿
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
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Text.Json.Serialization;

public class JailConfig : BasePluginConfig
{
    [JsonPropertyName("username")]
    public String username { get; set; } = "";

    [JsonPropertyName("password")]
    public String password { get; set; } = "";

    [JsonPropertyName("server")]
    public String server { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public String port { get; set; } = "3306";

    [JsonPropertyName("database")]
    public String database { get; set; } = "cs2_jail";

    [JsonPropertyName("mute_dead")]
    public bool mute_dead { get; set; } = true;

    [JsonPropertyName("warden_laser")]
    public bool warden_laser { get; set; } = true;

    [JsonPropertyName("ct_voice_only")]
    public bool ct_voice_only { get; set; } = false;

    [JsonPropertyName("thirty_sec_mute")]
    public bool thirty_sec_mute { get; set; } = true;

    [JsonPropertyName("mute_t_allways")]
    public bool mute_t_allways { get; set; } = false;

    [JsonPropertyName("warden_on_voice")]
    public bool warden_on_voice { get; set; } = true;

    [JsonPropertyName("ct_swap_only")]
    public bool ct_swap_only { get; set; } = false;

    [JsonPropertyName("ct_guns")]
    public bool ct_guns { get; set; } = true;

    [JsonPropertyName("ct_gun_menu")]
    public bool ct_gun_menu { get; set; } = true;

    [JsonPropertyName("ct_armour")]
    public bool ct_armour { get; set; } = true;

    [JsonPropertyName("warden_force_removal")]
    public bool warden_force_removal { get; set; } = true;

    [JsonPropertyName("strip_spawn_weapons")]
    public bool strip_spawn_weapons { get; set; } = true;

    [JsonPropertyName("warday_guns")]
    public bool warday_guns { get; set; } = false;

    // ratio of t to CT
    [JsonPropertyName("bal_guards")]
    public int bal_guards { get; set; } = 0;

    [JsonPropertyName("enable_riot")]
    public bool riot_enable { get; set; } = false;

    [JsonPropertyName("hide_kills")]
    public bool hide_kills { get; set; } = false;

    [JsonPropertyName("restrict_ping")]
    public bool restrict_ping { get; set; } = true;

    [JsonPropertyName("colour_rebel")]
    public bool colour_rebel { get; set; } = false;

    [JsonPropertyName("rebel_cant_lr")]
    public bool rebel_cant_lr { get; set; } = false;   

    [JsonPropertyName("lr_knife")]
    public bool lr_knife { get; set; } = true;

    [JsonPropertyName("lr_gun_toss")]
    public bool lr_gun_toss { get; set; } = true;

    [JsonPropertyName("lr_dodgeball")]
    public bool lr_dodgeball { get; set; } = true;

    [JsonPropertyName("lr_no_scope")]
    public bool lr_no_scope { get; set; } = true;

    [JsonPropertyName("lr_shotgun_war")]
    public bool lr_shotgun_war { get; set; } = true;

    [JsonPropertyName("lr_grenade")]
    public bool lr_grenade { get; set; } = true;

    [JsonPropertyName("lr_russian_roulette")]
    public bool lr_russian_roulette { get; set; } = true;

    [JsonPropertyName("lr_scout_knife")]
    public bool lr_scout_knife { get; set; } = true;

    [JsonPropertyName("lr_headshot_only")]
    public bool lr_headshot_only { get; set; } = true;

    [JsonPropertyName("lr_shot_for_shot")]
    public bool lr_shot_for_shot { get; set; } = true;

    [JsonPropertyName("lr_mag_for_mag")]
    public bool lr_mag_for_mag { get; set; } = true;

    [JsonPropertyName("lr_count")]
    public uint lr_count { get; set; } = 2;

    [JsonPropertyName("rebel_requirehit")]
    public bool rebel_requirehit { get; set; } = false;

    [JsonPropertyName("wsd_round")]
    public int wsd_round { get; set; } = 50;
}

// main plugin file, controls central hooking
// defers to warden, lr and sd
[MinimumApiVersion(141)]
public class JailPlugin : BasePlugin, IPluginConfig<JailConfig>
{
    // Global event settings, used to filter plugin activits
    // during warday and SD
    bool is_event_active = false;

    public JailConfig Config  { get; set; } = new JailConfig();

    public static bool is_warden(CCSPlayerController? player)
    {
        return warden.is_warden(player);
    }

    public static bool event_active()
    {
        return global_ctx.is_event_active;
    }

    public static void start_event()
    {
        global_ctx.is_event_active = true;
    }

    public static void end_event()
    {
        global_ctx.is_event_active = false;
    }

    public static void win_lr(CCSPlayerController? player,LastRequest.LRType type)
    {
        jail_stats.win(player,type);
    }

    public static void lose_lr(CCSPlayerController? player, LastRequest.LRType type)
    {
        jail_stats.loss(player,type);
    }

    public static void purge_player_stats(CCSPlayerController? player)
    {
        jail_stats.purge_player(player);
    }

    public override string ModuleName => "CS2 Jailbreak - destoer";

    public override string ModuleVersion => "v0.3.4";

    public override void Load(bool hotReload)
    {
        global_ctx = this;
        logs = new Logs(this); 

        register_commands();
        
        register_hook();

        register_listener();

        JailPlayer.setup_db();

        Console.WriteLine("Sucessfully started JB");

        AddTimer(Warden.LASER_TIME,warden.laser_tick,CSTimer.TimerFlags.REPEAT);

    }

    void stat_db_reload()
    {
        Task.Run(async () => 
        {
            var database = await jail_stats.connect_db();

            jail_stats.setup_db(database);
        });
    }

    public void OnConfigParsed(JailConfig config)
    {
        // give each sub plugin the config
        this.Config = config;
        
        jail_stats.config = config;
        lr.config = config;

        warden.config = config;
        warden.mute.config = config;
        warden.warday.config = config;
        JailPlayer.config = config;

        sd.config = config;

        lr.lr_config_reload();
        stat_db_reload();
    }

    void register_listener()
    {
        RegisterListener<Listeners.OnEntitySpawned>(entity =>
        {
            lr.ent_created(entity);
            sd.ent_created(entity);
        });
    }

    void add_localized_cmd(String base_name,String desc,CommandInfo.CommandCallback callback)
    {
        AddCommand("css_" + Localizer[base_name],desc,callback);
    }

    void register_commands()
    {
        // reg warden comamnds
        add_localized_cmd("warden.take_warden_cmd", "take warden", warden.take_warden_cmd);
        add_localized_cmd("warden.leave_warden_cmd", "leave warden", warden.leave_warden_cmd);
        add_localized_cmd("warden.remove_warden_cmd", "remove warden", warden.remove_warden_cmd);
        add_localized_cmd("warden.remove_marker_cmd","remove warden marker",warden.remove_marker_cmd);

        add_localized_cmd("warden.marker_colour_cmd", "set marker colour", warden.marker_colour_cmd);
        add_localized_cmd("warden.laser_colour_cmd", "set laser colour", warden.laser_colour_cmd);

        add_localized_cmd("warden.colour_cmd","set player colour",warden.colour_cmd);

        add_localized_cmd("warden.no_block_cmd","warden : disable block",warden.wub_cmd);
        add_localized_cmd("warden.block_cmd","warden : enable block",warden.wb_cmd);

        add_localized_cmd("warden.sd_cmd","warden : call a special day",sd.warden_sd_cmd);
        add_localized_cmd("warden.sd_ff_cmd","warden : call a friendly fire special day",sd.warden_sd_ff_cmd);

        add_localized_cmd("warden.swap_guard","admin : move a player to ct",warden.swap_guard_cmd);

        add_localized_cmd("warden.warday_cmd","warden : start warday",warden.warday_cmd);
        add_localized_cmd("warden.list_cmd", "warden : show all commands",warden.cmd_info);
        add_localized_cmd("warden.time_cmd","how long as warden been active?",warden.warden_time_cmd);

        add_localized_cmd("warden.gun_cmd","give ct guns",warden.cmd_ct_guns);

        add_localized_cmd("warden.force_open_cmd","force open every door and vent",warden.force_open_cmd);
        add_localized_cmd("warden.force_close_cmd","force close every door",warden.force_close_cmd);

        add_localized_cmd("warden.fire_guard_cmd","admin : Remove all guards apart from warden",warden.fire_guard_cmd);

        add_localized_cmd("warden.give_freeday_cmd","give t a freeday",warden.give_freeday_cmd);
        add_localized_cmd("warden.give_pardon_cmd","give t a freeday",warden.give_pardon_cmd);

        // reg lr commands
        add_localized_cmd("lr.start_lr_cmd","start an lr",lr.lr_cmd);
        add_localized_cmd("lr.cancel_lr_cmd","admin : cancel lr",lr.cancel_lr_cmd);
        add_localized_cmd("lr.stats_cmd","list lr stats",jail_stats.lr_stats_cmd);

        // reg sd commands
        add_localized_cmd("sd.start_cmd","start a sd",sd.sd_cmd);
        add_localized_cmd("sd.start_ff_cmd","start a ff sd",sd.sd_ff_cmd);
        add_localized_cmd("sd.cancel_cmd","cancel an sd",sd.cancel_sd_cmd);

        add_localized_cmd("logs.logs_cmd", "show round logs", logs.LogsCommand);

        // debug 
        if(Debug.enable)
        {
            AddCommand("nuke","debug : kill every player",Debug.nuke);
            AddCommand("is_rebel","debug : print rebel state to console",warden.is_rebel_cmd);
            AddCommand("lr_debug","debug : start an lr without restriction",lr.lr_debug_cmd);
            AddCommand("is_blocked","debug : print block state",warden.block.is_blocked);
            AddCommand("test_laser","test laser",Debug.test_laser);
            AddCommand("test_strip","test weapon strip",Debug.test_strip_cmd);
            AddCommand("join_ct_debug","debug : force join ct",Debug.join_ct_cmd);
            AddCommand("hide_weapon_debug","debug : hide player weapon on back",Debug.hide_weapon_cmd);
            AddCommand("rig","debug : force player to boss on sd",sd.sd_rig_cmd);
            AddCommand("is_muted","debug : print voice flags",Debug.is_muted_cmd);
            AddCommand("spam_db","debug : spam db",Debug.test_lr_inc);
            AddCommand("wsd_enable","debug : enable wsd",Debug.wsd_enable_cmd);
            AddCommand("test_noblock","debug : enable wsd",Debug.test_noblock_cmd);
        }
    }

    public HookResult join_team(CCSPlayerController? invoke, CommandInfo command)
    {
        jail_stats.load_player(invoke);

        JailPlayer? jail_player = warden.jail_player_from_player(invoke);

        if(jail_player != null)
        {
            jail_player.load_player(invoke);
        }        

        if(!warden.join_team(invoke,command))
        {
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    
    void register_hook()
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd,HookMode.Pre);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventTeamchangePending>(OnSwitchTeam);
        RegisterEventHandler<EventMapTransition>(OnMapChange);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath,HookMode.Pre);
        RegisterEventHandler<EventItemEquip>(OnItemEquip);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventWeaponZoom>(OnWeaponZoom);
        RegisterEventHandler<EventPlayerPing>(OnPlayerPing);

        // take damage causes crashes on windows
        // cant figure out why because the windows cs2 console wont log
        // before it dies
        if(!Lib.is_windows())
        {
            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage,HookMode.Pre);
        }
        
        HookEntityOutput("func_button", "OnPressed", OnButtonPressed);
        
        RegisterListener<Listeners.OnClientVoice>(OnClientVoice);
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);

        AddCommandListener("jointeam",join_team);
        AddCommandListener("player_ping",player_ping_cmd);

        // TODO: need to hook weapon drop
    }

    public HookResult player_ping_cmd(CCSPlayerController? invoke, CommandInfo command)
    {
        // if player is not warden ignore the ping
        if(Config.restrict_ping && !warden.is_warden(invoke))
        {
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerPing(EventPlayerPing  @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if(player.is_valid())
        {
            warden.ping(player,@event.X,@event.Y,@event.Z);
        }

        return HookResult.Continue;
    }

    void OnClientVoice(int slot)
    {
        CCSPlayerController? player = Utilities.GetPlayerFromSlot(slot);

        if(player.is_valid())
        {
            warden.voice(player);
        }
    }

    // button log
    HookResult OnButtonPressed(CEntityIOOutput output, String name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        CCSPlayerController? player = activator.player();

        // grab player controller from pawn
        CBaseEntity? ent =  Utilities.GetEntityFromIndex<CBaseEntity>((int)caller.Index);

        if(player.is_valid() && ent != null && ent.IsValid)
        {
            logs.AddLocalized(player, "logs.format.button", ent.Entity?.Name ?? "Unlabeled", output?.Connections?.TargetDesc ?? "None");
        }

        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage,HookMode.Pre);
    }

    HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player.is_valid())
        {
            lr.grenade_thrown(player);
            sd.grenade_thrown(player);
            logs.AddLocalized(player, "logs.format.grenade", @event.Weapon); 
        }

        return HookResult.Continue;
    }
  
    HookResult OnWeaponZoom(EventWeaponZoom @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player.is_valid())
        {
            lr.weapon_zoom(player);
        }

        return HookResult.Continue;
    }

    HookResult OnItemEquip(EventItemEquip @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player.is_valid())
        {
            lr.weapon_equip(player,@event.Item);
            sd.weapon_equip(player,@event.Item);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        int damage = @event.DmgHealth;
        int health = @event.Health;
        int hitgroup = @event.Hitgroup;

        if(player.is_valid())
        {
            lr.player_hurt(player,attacker,damage,health,hitgroup);
            warden.player_hurt(player,attacker,damage,health);
            sd.player_hurt(player,attacker,damage,health,hitgroup);
        }

        return HookResult.Continue;
    }

    HookResult OnTakeDamage(DynamicHook handle)
    {
        CEntityInstance victim = handle.GetParam<CEntityInstance>(0);
        CTakeDamageInfo damage_info = handle.GetParam<CTakeDamageInfo>(1);

        CHandle<CBaseEntity> dealer = damage_info.Attacker;

        // get player and attacker
        CCSPlayerController? player = victim.player();
        CCSPlayerController? attacker = dealer.player();

        if(player.is_valid())
        {
            sd.take_damage(player,attacker,ref damage_info.Damage);
            lr.take_damage(player,attacker,ref damage_info.Damage);
        }
        
        return HookResult.Continue;
    }

    HookResult OnMapChange(EventMapTransition @event, GameEventInfo info)
    {
        warden.map_start();

        return HookResult.Continue;
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        warden.round_start();
        lr.round_start();
        sd.round_start();

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;
        CCSPlayerController? killer = @event.Attacker;

        // hide t killing ct
        if(Config.hide_kills && killer.is_t() && victim.is_ct())
        {
            //@event.Attacker = player;
            // fire event as is to T
            foreach(CCSPlayerController? player in Utilities.GetPlayers())
            {
                if(player.is_valid())
                {
                    if(player.is_t())
                    {
                        // T gets full event
                        @event.Userid = victim;
                        @event.Attacker = killer;

                        @event.FireEventToClient(player);
                    }

                    else
                    {
                        // ct gets a suicide
                        @event.Userid = victim;
                        @event.Attacker = victim;
                        @event.Assister = victim;

                        @event.FireEventToClient(player);
                    }
                }
            }

            info.DontBroadcast = true;
        }


        if(victim != null && victim.is_valid())
        {
            warden.death(victim,killer);
            lr.death(victim);
            sd.death(victim,killer);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player.is_valid())
        {
            int slot = player.Slot;

            AddTimer(0.5f,() =>  
            {
                warden.spawn(Utilities.GetPlayerFromSlot(slot));
            });
            
        }

        return HookResult.Continue;
    }

    HookResult OnSwitchTeam(EventTeamchangePending @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        int new_team = @event.Toteam;

        if(player.is_valid())
        {
            warden.switch_team(player,new_team);
        }

        return HookResult.Continue;
    }

    public void OnClientAuthorized(int slot, SteamID steamid)
    {
        CCSPlayerController? player = Utilities.GetPlayerFromSlot(slot);

        if(player.is_valid())
        {
            // load in player stats
            jail_stats.load_player(player);
            
            JailPlayer? jail_player = warden.jail_player_from_player(player);

            if(jail_player != null)
            {
                jail_player.load_player(player);
            }
        }
    }

    HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player.is_valid())
        {
            warden.disconnect(player);
            lr.disconnect(player);
            sd.disconnect(player);
        }

        return HookResult.Continue;
    }

    HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        warden.round_end();
        lr.round_end();
        sd.round_end();

        return HookResult.Continue;
    }

    HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        // attempt to get player and weapon
        var player = @event.Userid;
        String name = @event.Weapon;

        if(player.is_valid_alive())
        {
            warden.weapon_fire(player,name);
            lr.weapon_fire(player,name);
        }

        return HookResult.Continue;
    }

    public static String localize(string name,params Object[] args)
    {
        return String.Format(global_ctx.Localizer[name],args);
    }

    public static Warden warden = new Warden();
    public static LastRequest lr = new LastRequest();
    public static SpecialDay sd = new SpecialDay();
    public static JailStats jail_stats = new JailStats();

    // in practice these wont be null
    #pragma warning disable CS8618 
    public static Logs logs;

    // workaround to query global state!
    public static JailPlugin global_ctx;

    #pragma warning restore CS8618
}