using System;
using System.IO;
using System.Threading;
using FiveVibe;

internal static class AiConnectionCheck
{
    public static int Main(string[] args)
    {
        try
        {
            if ((args.Length != 1 && args.Length != 2) || !File.Exists(args[0]))
            { Console.WriteLine("Provide the local env file path."); return 2; }
            var config = AiConfiguration.Load(args[0], "gpt-4.1-mini");
            if (!config.HasApiKey) { Console.WriteLine("No API key configured in the env file. No request sent."); return 2; }
            using (var planner = new OpenAiPlanner(config.ApiKey, config.Model))
            {
                if(args.Length==2 && args[1]=="--ambient")
                {
                    string[] prompts={"andiamo a ls custom","prendi una moto","prendi una jeep e facciamo un giro","Portami a LS Customs con un SUV","Sincronizza meteo e ora con quelli reali","Disattiva solo il meteo reale"};
                    string[] actions={"place","vehicle","cruise","place","realtime_both","realtime_weather_off"};
                    string[] types={"any","motorcycle","offroad","suv","any","any"};
                    for(int i=0;i<prompts.Length;i++)
                    {
                        var ambientPlan=planner.PlanAsync(prompts[i],"SYNTHETIC TEST: character=michael; driving=idle; in_vehicle=false; spectator_camera=Gameplay; mission_active=false",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                        bool ok=ambientPlan.Action==actions[i] && ambientPlan.VehicleType==types[i] && ((i!=0 && i!=3) || ambientPlan.Place=="ls_customs");
                        Console.WriteLine("Ambient case "+(i+1)+": "+ambientPlan.Action+" / "+ambientPlan.Place+" / "+ambientPlan.VehicleType+" / "+(ok?"OK":"UNEXPECTED"));
                        if(!ok)return 3;
                    }
                    Console.WriteLine("Ambient interpretation passed. No action sent to GTA.");return 0;
                }
                if(args.Length==2 && args[1]=="--camera")
                {
                    string[] prompts={"Vorrei guardare il tragitto come un film, con riprese dinamiche. Continua pure a guidare.","Metti la visuale in prima persona.","Passa alla telecamera cinema originale di GTA.","Torna alla camera normale, senza fermare il viaggio."};
                    string[] expected={"camera_cinema","camera_first","camera_native","camera_gameplay"};
                    for(int i=0;i<prompts.Length;i++)
                    {
                        var cameraPlan=planner.PlanAsync(prompts[i],"SYNTHETIC TEST: character=michael; driving=driving; in_vehicle=true; spectator_camera=Gameplay; mission_active=false",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                        bool ok=cameraPlan.Action==expected[i] && cameraPlan.Actor=="current";
                        Console.WriteLine("Camera case "+(i+1)+": "+cameraPlan.Action+" / "+(ok?"OK":"UNEXPECTED"));
                        if(!ok) return 3;
                    }
                    Console.WriteLine("Camera interpretation passed. No action sent to GTA."); return 0;
                }
                if(args.Length==2 && args[1]=="--mission")
                {
                    string[] prompts={"Completa da solo Franklin e Lamar, leggi gli obiettivi e prosegui fino alla fine.","Start Mission Pilot for Franklin and Lamar.","Completa il Prologo da solo.","A che punto sei con la missione?"};
                    for(int i=0;i<prompts.Length;i++)
                    {
                        var missionPlan=planner.PlanAsync(prompts[i],"SYNTHETIC TEST: character=franklin; driving=idle; in_vehicle=false; supported_mission_running=true; mission_pilot=active; mission_pilot_stage=Follow Lamar; observed_objective_label=AR1_CHASE",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                        bool ok=i<2 ? missionPlan.Action=="mission_auto" && (missionPlan.Actor=="current" || missionPlan.Actor=="franklin") : i==2 ? missionPlan.Action=="say" : missionPlan.Action=="mission_status";
                        Console.WriteLine("Mission case "+(i+1)+": "+missionPlan.Action+" / "+(ok?"OK":"UNEXPECTED"));
                        if(!ok) return 3;
                    }
                    Console.WriteLine("Mission interpretation passed. No action sent to GTA."); return 0;
                }
                if(args.Length==2 && args[1]=="--world-ui")
                {
                    string[] prompts={"Metti pioggia e imposta l'ora alle 22:30.","Nascondi il riquadro della task attiva, continua pure a guidare.","Freeze the clock.","Rimetti il meteo automatico e fai ripartire il tempo."};
                    for(int i=0;i<prompts.Length;i++)
                    {
                        var worldPlan=planner.PlanAsync(prompts[i],"SYNTHETIC TEST: character=michael; driving=driving; in_vehicle=true; mission_active=false; task_hud_visible=true; clock_paused=false",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                        bool ok=i==0 ? worldPlan.Action=="world_settings" && worldPlan.WeatherType=="rain" && worldPlan.Hour==22 && worldPlan.Minute==30 :
                            i==1 ? worldPlan.Action=="hud_hide" : i==2 ? worldPlan.Action=="world_settings" && worldPlan.Clock=="pause" : worldPlan.Action=="world_settings" && worldPlan.WeatherType=="auto" && worldPlan.Clock=="resume";
                        Console.WriteLine("World/UI case "+(i+1)+": "+worldPlan.Action+" / "+(ok?"OK":"UNEXPECTED"));
                        if(!ok) return 3;
                    }
                    Console.WriteLine("World/UI interpretation passed. No action sent to GTA.");return 0;
                }
                if (args.Length==2 && args[1]=="--places")
                {
                    string[] texts={"Portami a Paleto Bay, guida senza fretta.","Let's take a walk to Mirror Park.","Drive to Sandy Shores Airfield.","Mi piacerebbe andare al molo con la ruota panoramica.","Take me to Atlantis.","Go to Michael's house."};
                    string[] actions={"place","walk_place","place","place","say","place"};
                    string[] places={"paleto_bay","mirror_park","sandy_airfield","del_perro_pier","none","michael_home"};
                    for(int i=0;i<texts.Length;i++) {
                        string actor=i==5?"franklin":"michael";
                        var result=planner.PlanAsync(texts[i],"SYNTHETIC TEST: character="+actor+"; in_vehicle=true; driving=idle; target_speed_kmh=45; mission_active=false",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                        bool correct=result.Action==actions[i] && result.Place==places[i] && (result.Actor=="current" || result.Actor==actor);
                        Console.WriteLine("Place case "+(i+1)+": "+result.Action+" / "+result.Place+" / actor="+result.Actor+" / "+(correct?"OK":"UNEXPECTED"));
                        if(!correct)return 3;
                    }
                    Console.WriteLine("Named-place checks passed. No action sent to GTA."); return 0;
                }
                if (args.Length == 2 && (args[1] == "--expanded" || args[1] == "--npc-mission"))
                {
                    string context = "SIMULAZIONE DI COLLAUDO. personaggio=michael; guida=idle; in_auto=false; velocita_obiettivo_kmh=45; " +
                        "missione_attiva=false; telefono_visibile=false; chiamata_in_corso=false; npc_vicino=5 metri; npc_davanti=5 metri; " +
                        "npc_precedente_disponibile=true; missione_disponibile=Lester a 200 metri";
                    string[] prompts = { "Facciamo due passi qui intorno, senza prendere la macchina.", "Passa a Trevor.", "Sta suonando il cellulare, rispondi per favore.", "Vai da quella persona davanti a noi e salutala.", "Raggiungi il punto della missione di Lester." };
                    string[] expected = { "walk_wander", "switch_character", "phone_answer", "greet_npc", "mission" };
                    for (int i = args[1] == "--npc-mission" ? 3 : 0; i < prompts.Length; i++)
                    {
                        var result = planner.PlanAsync(prompts[i], context, new string[0], CancellationToken.None).GetAwaiter().GetResult();
                        bool correct = result.Action == expected[i] && (i != 1 || result.Actor == "trevor") && (i != 3 || result.Target == "ahead") && (i != 4 || result.Target == "lester");
                        Console.WriteLine("Verifica " + (i + 1) + ": " + result.Action + " / " + (correct ? "OK" : "DIVERSA DAL PREVISTO"));
                        if (!correct) return 3;
                    }
                    Console.WriteLine("Expanded actions interpreted by OpenAI. No action sent to the game.");
                    return 0;
                }
                // Synthetic context only: this diagnostic never calls the game.
                var plan = planner.PlanAsync("Prendiamo un'auto e portami a fare un giretto senza fretta, giusto per rilassarci.",
                    "SIMULAZIONE DI COLLAUDO, nessun gioco controllato. personaggio=michael; guida=idle; velocita_obiettivo_kmh=45; in_auto=true",
                    new string[0], CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("OpenAI ha risposto con un piano valido: " + plan.Action + ", velocita obiettivo " + plan.SpeedKmh + " km/h.");
                Console.WriteLine("Chiave non visualizzata. Nessuna azione inviata al gioco.");
                return plan.Action == "cruise" ? 0 : 3;
            }
        }
        catch (Exception error)
        {
            // Planner and config errors contain no request headers, response bodies or key values.
            Console.Error.WriteLine(error is FormatException || error is InvalidOperationException ||
                (error is ArgumentException && (error.Message.StartsWith("Mission Pilot", StringComparison.Ordinal) || error.Message=="World settings do not match the action." || error.Message=="Vehicle selection does not match the action." || error.Message=="Invalid weather or clock settings.")) ? error.Message :
                "Connection check failed ("+error.GetType().Name+"). Check the connection and local env configuration.");
            return 1;
        }
    }
}
