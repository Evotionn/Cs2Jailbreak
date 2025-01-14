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

using CSTimer = CounterStrikeSharp.API.Modules.Timers;

public partial class LastRequest
{
    public void player_hurt(CCSPlayerController? player, CCSPlayerController? attacker, int damage,int health, int hitgroup)
    {
        // check no damage restrict
        LRBase? lr = find_lr(player);

        // no lr
        if(lr == null)
        {
            return;
        }
        
        // not a pair
        if(!is_pair(player,attacker))
        {
            return;
        }

        lr.player_hurt(damage,health,hitgroup);
    }

    public void take_damage(CCSPlayerController? player, CCSPlayerController? attacker, ref float damage)
    {
        // neither player is in lr we dont care
        if(!in_lr(player) && !in_lr(attacker))
        {
            return;
        }

        // not a pair restore hp
        if(!is_pair(player,attacker))
        {
            damage = 0.0f;
            return;
        }

        // check no damage restrict
        LRBase? lr = find_lr(player);

        if(lr == null)
        {
            return;
        }

        if(!lr.take_damage())
        {
            damage = 0.0f;
        }   
    }

    public void weapon_equip(CCSPlayerController? player,String name) 
    {
        if(!player.is_valid_alive())
        {
            return;
        }

        if(rebel_type == RebelType.KNIFE && !name.Contains("knife"))
        {
            player.strip_weapons();
            return;
        }

        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            CCSPlayerPawn? pawn = player.pawn();

            if(pawn == null)
            {
                return;
            }

            // strip all weapons that aint the restricted one
            var weapons = pawn.WeaponServices?.MyWeapons;

            if(weapons == null)
            {
                return;
            }

            foreach (var weapon_opt in weapons)
            {
                CBasePlayerWeapon? weapon = weapon_opt.Value;

                if (weapon == null)
                { 
                    continue;
                }
                
                var weapon_name = weapon.DesignerName;

                // TODO: Ideally we should just deny the equip all together but this works well enough
                if(!lr.weapon_equip(weapon_name))
                {
                    //Server.PrintToChatAll($"drop player gun: {player.PlayerName} : {weapon_name}");
                    player.DropActiveWeapon();
                }
            }    
        }
    }

    // couldnt get pulling the owner from the projectile ent working
    // so instead we opt for this
    public void weapon_zoom(CCSPlayerController? player)
    {
        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            lr.weapon_zoom();
        }       
    }

    // couldnt get pulling the owner from the projectile ent working
    // so instead we opt for this
    public void grenade_thrown(CCSPlayerController? player)
    {
        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            lr.grenade_thrown();
        }       
    }

    public void ent_created(CEntityInstance entity)
    {
        for(int l = 0; l < active_lr.Length; l++)
        {
            LRBase? lr = active_lr[l];

            if(lr != null && entity.IsValid)
            {
                lr.ent_created(entity);
            }
        }
    }

    public void round_start()
    {
        start_timestamp = Lib.cur_timestamp();

        purge_lr();
    }

    public void round_end()
    {
        purge_lr();
    }

    public void disconnect(CCSPlayerController? player)
    {
        JailPlugin.purge_player_stats(player);

        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            Chat.announce(LR_PREFIX,"Player disconnection cancelling LR");
            end_lr(lr.slot);
        }
    }

    public bool weapon_drop(CCSPlayerController? player,String name) 
    {
        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            return lr.weapon_drop(name);
        }

        return true;
    }

    public void weapon_fire(CCSPlayerController? player,String name) 
    {
        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            lr.weapon_fire(name);
        }
    }

    public void death(CCSPlayerController? player)
    {
        if(Lib.alive_t_count() == config.lr_count && player.is_t())
        {
            Chat.localize_announce(LR_PREFIX,"lr.ready");
        }


        LRBase? lr = find_lr(player);

        if(lr != null)
        {
            lr.lose();
        }
    }

}