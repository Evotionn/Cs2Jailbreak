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
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using System.Drawing;
using System.Text;
using System.Diagnostics.CodeAnalysis;

// NOTE: this is a timer wrapper, and should be owned the class
// wanting to use the timer
public class Countdown<T>
{
    public void start(String countdown_name, int countdown_delay,
        T countdown_data,Action<T,int>? countdown_print_func, Action <T> countdown_callback)
    {
        this.delay = countdown_delay;
        this.callback = countdown_callback;
        this.name = countdown_name;
        this.data = countdown_data;
        this.print_func = countdown_print_func;

        this.handle = JailPlugin.global_ctx.AddTimer(1.0f,countdown,CSTimer.TimerFlags.STOP_ON_MAPCHANGE | CSTimer.TimerFlags.REPEAT);
    }

    public void kill()
    {
       Lib.kill_timer(ref handle);
    }

    void countdown()
    {
        delay -= 1;

        // countdown over
        if(delay <= 0)
        {
            // kill the timer
            // and then call the callback
            kill();

            if(callback != null && data != null)
            {
                callback(data);
            }
        }

        // countdown still active
        else
        {
            // custom print
            if(print_func != null && data != null)
            {
                print_func(data,delay);
            }

            // default print
            else
            {
                Chat.print_centre_all($"{name} is starting in {delay} seconds");
            }
        }
    }

    public int delay = 0;
    public Action<T>? callback = null;
    public String name = "";
    public Action<T,int>? print_func = null;
    CSTimer.Timer? handle = null;

    // callback data
    T? data = default(T);
}

    

public static class Lib
{
    static public bool is_windows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    static public void invoke_player_menu(CCSPlayerController? invoke, String name,
        Action<CCSPlayerController, ChatMenuOption> callback, Func<CCSPlayerController?,bool> filter)
    {
        if(!invoke.is_valid())
        {
            return;
        }

        ChatMenu menu = new ChatMenu(name);

        foreach(var player in Utilities.GetPlayers())
        {
            if(filter(player))
            {
                menu.AddMenuOption(player.PlayerName, callback);
            }
        }

        ChatMenus.OpenMenu(invoke, menu); 
    }

    public static void colour_menu(CCSPlayerController? player,Action<CCSPlayerController, ChatMenuOption> callback, String name)
    {
        if(!player.is_valid())
        {
            return;
        }

        var colour_menu = new ChatMenu(name);

        foreach(var item in Lib.COLOUR_CONFIG_MAP)
        {
            colour_menu.AddMenuOption(item.Key, callback);
        }

        ChatMenus.OpenMenu(player, colour_menu);    
    }

    static public void play_sound_all(String sound)
    {
        foreach(CCSPlayerController? player in Utilities.GetPlayers())
        {
            player.play_sound(sound);
        }
    }

    static public void mute_t()
    {
        foreach(CCSPlayerController player in Utilities.GetPlayers())
        {
            if(player.is_valid() && player.is_t())
            {
                player.mute();
            }
        }
    }

    static public void kill_timer(ref CSTimer.Timer? timer)
    {
        if(timer != null)
        {
            timer.Kill();
            timer = null;
        }
    }

    static public void unmute_all()
    {
        foreach(CCSPlayerController player in Utilities.GetPlayers())
        {
            if(player.is_valid())
            {
                player.unmute();
            }
        }
    }

    static public long cur_timestamp()
    {
        return DateTimeOffset.Now.ToUnixTimeSeconds();
    }


    static public void enable_friendly_fire()
    {
        if(ff != null)
        {
            ff.SetValue(true);
        }
    }

    static public void disable_friendly_fire()
    {
        if(ff != null)
        {
            ff.SetValue(false);
        }
    }

    static public void swap_all_t()
    {
        // get valid players
        List<CCSPlayerController> players = Utilities.GetPlayers();
        var valid = players.FindAll(player => player.is_valid_alive());

        foreach(var player in valid)
        {
            player.SwitchTeam(CsTeam.Terrorist);
        }
    }

    static public List<CCSPlayerController> get_alive_ct()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        return players.FindAll(player => player.is_valid_alive() && player.is_ct());
    }

    static public int ct_count()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        return players.FindAll(player => player.is_valid() && player.is_ct()).Count;        
    }

    static public int t_count()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        return players.FindAll(player => player.is_valid() && player.is_t()).Count;        
    }

    static public int alive_ct_count()
    {
        return get_alive_ct().Count;
    }

    static public List<CCSPlayerController> get_alive_t()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        return players.FindAll(player => player.is_valid_alive() && player.is_t());;
    }

    static public int alive_t_count()
    {
        return get_alive_t().Count;
    }

    static public bool block_enabled()
    {
        if(block_cvar != null)
        {
            return block_cvar.GetPrimitiveValue<int>() == 1;
        }

        return true;
    }

    static public void block_all()
    {
        if(block_cvar != null)
        {
            block_cvar.SetValue(1);
        }
    }

    static public void unblock_all()
    {
        if(block_cvar != null)
        {
            block_cvar.SetValue(0);
        }
    }

    
    static public void set_cvar_str(String name, String value)
    {
        // why doesn't this work lol
        
        ConVar? cvar = ConVar.Find(name);

        if(cvar != null)
        {
            cvar.StringValue = value;
        }
    }

    static public bool is_active_team(int team)
    {
        return (team == Player.TEAM_T || team == Player.TEAM_CT);
    }

    // TODO: just go with a simple print for now
    static public void log(String str)
    {
        Console.WriteLine($"[JAILBREAK]: {str}");
    }


    public static readonly Color CYAN = Color.FromArgb(255, 153, 255, 255);
    public static readonly Color RED = Color.FromArgb(255, 255, 0, 0);
    public static readonly Color INVIS = Color.FromArgb(0, 255, 255, 255);
    public static readonly Color GREEN = Color.FromArgb(255,0, 191, 0);

    public static readonly Dictionary<string,Color> COLOUR_CONFIG_MAP = new Dictionary<string,Color>()
    {
        {"Cyan",Lib.CYAN}, // cyan
        {"Pink",Color.FromArgb(255,255,192,203)} , // pink
        {"Red",Lib.RED}, // red
        {"Purple",Color.FromArgb(255,118, 9, 186)}, // purple
        {"Grey",Color.FromArgb(255,66, 66, 66)}, // grey
        {"Green",GREEN}, // green
        {"Yellow",Color.FromArgb(255,255, 255, 0)} // yellow
    };

    static ConVar? block_cvar = ConVar.Find("mp_solid_teammates");
    static ConVar? ff = ConVar.Find("mp_teammates_are_enemies");

    public const int HITGROUP_HEAD = 0x1;
}