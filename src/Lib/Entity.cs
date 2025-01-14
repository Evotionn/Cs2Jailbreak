using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;

public static class Entity
{
    static public void remove(int index, String name)
    {
        CBaseEntity? ent = Utilities.GetEntityFromIndex<CBaseEntity>(index);

        if(ent != null && ent.DesignerName == name)
        {
            ent.Remove();
        }
    }

    static public void remove_delay(this CEntityInstance entity, float delay, String name)
    {
        // remove projectile
        if(entity.DesignerName == name)
        {
            int index = (int)entity.Index;

            JailPlugin.global_ctx.AddTimer(delay,() => 
            {
                remove(index,name);
            });
        }
    }

    static void force_ent_input(String name, String input)
    {
        // search for door entitys and open all of them!
        var target = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name);

        foreach(var ent in target)
        {
            if(!ent.IsValid)
            {
                continue;
            }

            ent.AcceptInput(input);
        }
    }

    // TODO: is their a cheaper way to do this?
    static public int ent_count()
    {
        return Utilities.GetAllEntities().Count();
    }


    static Vector VEC_ZERO = new Vector(0.0f,0.0f,0.0f);
    static QAngle ANGLE_ZERO = new QAngle(0.0f,0.0f,0.0f);

    static public void move(this CEnvBeam? laser,Vector start, Vector end)
    {
        if(laser == null)
        {
            return;
        }

        // set pos
        laser.Teleport(start, ANGLE_ZERO, VEC_ZERO);

        // end pos
        // NOTE: we cant just move the whole vec
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;

        Utilities.SetStateChanged(laser,"CBeam", "m_vecEndPos");
    }

    static public void move_laser_by_index(int laser_index,Vector start, Vector end)
    {
        CEnvBeam? laser = Utilities.GetEntityFromIndex<CEnvBeam>(laser_index);
        if(laser != null && laser.DesignerName == "env_beam")
        {
            laser.move(start,end);
        }
    }

    static public void set_colour(this CEnvBeam? laser, Color colour)
    {
        if(laser != null)
        {
            laser.Render = colour;
        }
    }


    static public int draw_laser(Vector start, Vector end, float width, Color colour)
    {
        CEnvBeam? laser = Utilities.CreateEntityByName<CEnvBeam>("env_beam");

        if(laser == null)
        {
            return -1;
        }

        // setup looks
        laser.set_colour(colour);
        laser.Width = 2.0f;

        // circle not working?
        //laser.Flags |= 8;

        laser.move(start,end);

        // start spawn
        laser.DispatchSpawn(); 

        return (int)laser.Index;
    }

    static String DOOR_PREFIX =  $" {ChatColors.Green}[Door control]: {ChatColors.White}";

    public static void force_close()
    {
        Chat.announce(DOOR_PREFIX,"Forcing closing all doors!");

        force_ent_input("func_door","Close");
        force_ent_input("func_movelinear","Close");
        force_ent_input("func_door_rotating","Close");
        force_ent_input("prop_door_rotating","Close");
    }

    public static void force_open()
    {
        Chat.announce(DOOR_PREFIX,"Forcing open all doors!");

        force_ent_input("func_door","Open");
        force_ent_input("func_movelinear","Open");
        force_ent_input("func_door_rotating","Open");
        force_ent_input("prop_door_rotating","Open");
        force_ent_input("func_breakable","Break");
    }


    static public CCSPlayerController? player(this CEntityInstance? instance)
    {
        if(instance == null)
        {
            return null;
        }

        // grab the pawn index
        int player_index = (int)instance.Index;

        // grab player controller from pawn
        CCSPlayerPawn? player_pawn =  Utilities.GetEntityFromIndex<CCSPlayerPawn>(player_index);

        // pawn valid
        if(player_pawn == null || !player_pawn.IsValid)
        {
            return null;
        }

        // controller valid
        if(player_pawn.OriginalController == null || !player_pawn.OriginalController.IsValid)
        {
            return null;
        }

        // any further validity is up to the caller
        return player_pawn.OriginalController.Value;
    }

    static public CCSPlayerController? player(this CHandle<CBaseEntity> handle)
    {
        if(handle.IsValid)
        {
            CBaseEntity? ent = handle.Value;

            if(ent != null)
            {
                return handle.Value.player();
            }
        }

        return null;
    }
}