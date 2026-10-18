// Compile the actual overlay against a recording native adapter, without GTA.
// Rendered previews use approximate Windows font metrics, not GTA screenshots.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using FiveVibe;

internal static class OverlayTests
{
    private static int passed;
    private static void Check(bool value,string name) {if(!value)throw new Exception(name);passed++;}
    private static void Key(ChatOverlay ui,Keys key) {ui.Key(new KeyEventArgs(key));}
    private static void Select(ChatOverlay ui,int tab) {Key(ui,Keys.Escape);ui.IsOpen=true;for(int i=0;i<tab;i++)Key(ui,Keys.Control|Keys.Right);}
    private static void Draw(ChatOverlay ui) {NativeFrame.Begin();ui.Draw("F7","F8");}
    private static void Click(ChatOverlay ui,NativeOp text)
    {NativeFrame.Begin();NativeFrame.MouseX=text.X+.002f;NativeFrame.MouseY=text.Y+.006f;NativeFrame.Click=true;ui.Draw("F7","F8");}
    private static void Bounds(HudLayout box,int tab)
    {
        float bottom=tab==3 || tab==5?box.MenuBottom:box.HistoryBottom;
        Check(NativeFrame.Ops.Where(o=>o.Text==null && o.Y>=box.HistoryTop && o.Y<box.QuickTop && o.H>=.029f && o.H<=.043f)
            .All(o=>o.Y+o.H<=bottom+.00001f),"Menu controls stay above the reserved footer: "+tab);
        Check(NativeFrame.Ops.Where(o=>o.Text!=null && o.Y>=box.HintsTop)
            .All(o=>o.X>=box.Left && o.X+o.W<=box.Right+.0001f && o.Y+o.H<box.Top+box.Height),"Complete key hints fit within panel");
        Check(new[]{"ENTER","TAB","ESC","F8"}.All(NativeFrame.Has),"Full keyboard labels are drawn without Scaleform glyph truncation");
    }
    public static int Main(string[] args)
    {
        try
        {
            foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f,32f/9f})foreach(float safe in new[]{.8f,.9f,1f})
            {
                NativeFrame.Aspect=aspect;NativeFrame.Safe=safe;
                var box=new HudLayout(aspect,safe);
                Check(box.HintsTop+.025f<box.Top+box.Height && box.MenuBottom<box.MenuScrollTop && box.MenuScrollTop+.021f<box.QuickTop,"Reserved regions remain separate");
                using(var ui=new ChatOverlay {IsOpen=true,InterfaceSounds=false,WorldTime="22:30",WeatherName="Overcast"})
                {
                    for(int tab=0;tab<6;tab++)
                    {
                        Select(ui,tab);Draw(ui);Bounds(box,tab);
                        if(tab==1 || tab==2) {Key(ui,Keys.PageDown);Draw(ui);Bounds(box,tab);}
                        if(tab==3 || tab==5)
                        {
                            for(int tick=0;tick<6;tick++)
                            {
                                NativeFrame.Begin();NativeFrame.MouseX=box.Left+box.Padding;NativeFrame.MouseY=box.HistoryTop+.1f;NativeFrame.Wheel=-1;
                                ui.Draw("F7","F8");Bounds(box,tab);
                            }
                            Check(NativeFrame.Has(tab==3?"Set weather city...":"Auto cinema while AI drives: ON"),"Bottom control reachable with wheel: "+tab);
                            Key(ui,Keys.PageUp);Draw(ui);Check(NativeFrame.Has(tab==3?"Clear":"Gameplay camera"),"PgUp returns to beginning: "+tab);
                            Key(ui,Keys.PageDown);Draw(ui);Check(NativeFrame.Has(tab==3?"Set weather city...":"Auto cinema while AI drives: ON"),"PgDn reaches last controls: "+tab);
                        }
                    }
                }
            }
            NativeFrame.Aspect=16f/9f;NativeFrame.Safe=.9f;
            var layout=new HudLayout(NativeFrame.Aspect,NativeFrame.Safe);
            using(var ui=new ChatOverlay {IsOpen=true,InterfaceSounds=false})
            {
                Select(ui,5);Draw(ui);
                Check(!NativeFrame.Has("Auto cinema while AI drives: ON"),"Hidden camera button is not drawn under quick requests");
                NativeFrame.Begin();NativeFrame.MouseX=layout.Left+layout.Padding+.01f;NativeFrame.MouseY=layout.Top+.555f;NativeFrame.Click=true;
                ui.Draw("F7","F8");Check(ui.TakeUiPlan()==null,"Clicking quick requests cannot activate the hidden camera toggle");
                Key(ui,Keys.PageDown);Draw(ui);var auto=NativeFrame.Find("Auto cinema while AI drives: ON");Click(ui,auto);
                Check(ui.TakeUiPlan().Action=="camera_auto_off","Visible camera toggle still dispatches its original action");
                Draw(ui);Click(ui,NativeFrame.Find("^"));Draw(ui);
                Check(NativeFrame.Has("Gameplay camera"),"On-screen scroll-up control works");
                Click(ui,NativeFrame.Find("v"));Draw(ui);Check(!NativeFrame.Has("Gameplay camera"),"On-screen scroll-down control works");
                Select(ui,3);Draw(ui);Check(!NativeFrame.Has("Set weather city..."),"World bottom controls start outside the viewport");
                Key(ui,Keys.PageDown);Draw(ui);Click(ui,NativeFrame.Find("Set weather city..."));
                Check(ui.Input=="Set weather location to ","City control focuses existing chat command entry");
                ui.Input+="Rome, Italy";string request=ui.Key(new KeyEventArgs(Keys.Enter));string city;
                Check(RealWorldCommands.TryCity(request,out city) && city=="Rome, Italy","City entry submits the original local weather-location command");
                Draw(ui);Check(NativeFrame.Has("Close"),"Escape hint says Close on chat");
                Key(ui,Keys.Control|Keys.Right);Draw(ui);Check(NativeFrame.Has("Back"),"Escape hint describes returning from submenu");
                Key(ui,Keys.Escape);Check(ui.IsOpen,"Escape returns from submenu without closing chat");
                Key(ui,Keys.Tab);Check(ui.Input=="Take a chill drive","Tab quick requests still work");
                Key(ui,Keys.Escape);Check(!ui.IsOpen,"Escape closes chat from chat tab");
                ui.Autonomous=true;Draw(ui);Check(NativeFrame.Has("F7") && NativeFrame.Has("F8"),"Closed-chat hints retain configured controls");
                ui.ShowDrivingHud=false;Draw(ui);Check(NativeFrame.Ops.Count==0,"Hiding task HUD also hides closed-chat hints");
            }
            var viewport=new HudMenuViewport(.2f,.5f,.7f,99);
            Check(Math.Abs(viewport.Offset-.2f)<.00001f && !viewport.Contains(.49f,.03f) && !viewport.Contains(.19f,.03f) && viewport.Contains(.47f,.03f),"Viewport clamps scrolling and excludes partially visible controls");
            foreach(string takeover in new[]{"F8","NumPad9","PageDown"})
            {
                var row=HudHintRow.Fit(new[]{"ENTER","TAB","ESC",takeover},new[]{"Send","Quick","Back","Take over"},layout.TextWidth,layout.Aspect,NativeFrame.Measure);
                Check(row.Width<=layout.TextWidth && row.Scale>=.15f,"Complete custom key names fit at a readable scale");
            }
            if(args.Length==1 && args[0]=="--render")
            {
                foreach(int tab in new[]{3,5})using(var ui=new ChatOverlay {IsOpen=true,InterfaceSounds=false,WorldTime="22:30",WeatherName="Overcast"})
                {
                    Select(ui,tab);Draw(ui);NativeFrame.Save("build/overlay-"+tab+"-top.png");
                    Key(ui,Keys.PageDown);Draw(ui);NativeFrame.Save("build/overlay-"+tab+"-bottom.png");
                }
            }
            Console.WriteLine(passed+" overlay checks passed (actual draw/interaction code with recording natives; gameplay still requires GTA).");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("After "+passed+" overlay checks: "+e);return 1;}
    }
}

internal sealed class NativeOp
{
    public string Text;public float X,Y,W,H,Scale;public Color Color;public int Font;
}
internal static class NativeFrame
{
    public static float Aspect=16f/9f,Safe=.9f,MouseX,MouseY;
    public static bool Click;public static int Wheel;
    public static readonly List<NativeOp> Ops=new List<NativeOp>();
    private static float scale;private static int font,justify;private static Color color;private static string text="";
    private const int Height=1080;
    private static int Width {get{return (int)Math.Round(Height*Aspect);}}
    public static void Begin() {Ops.Clear();MouseX=MouseY=0;Click=false;Wheel=0;}
    public static bool Has(string value) {return Ops.Any(o=>o.Text==value);}
    public static NativeOp Find(string value) {return Ops.First(o=>o.Text==value);}
    private static Font Font(float size,int id) {return new Font(id==1?"Segoe Script":"Segoe UI",size*60,FontStyle.Regular,GraphicsUnit.Pixel);}
    public static float Measure(string value,float size)
    {using(var bmp=new Bitmap(1,1))using(var g=Graphics.FromImage(bmp))using(var f=Font(size,0))return g.MeasureString(value,f,int.MaxValue,StringFormat.GenericTypographic).Width/Width;}
    public static object Call(GTA.Native.Hash hash,object[] a)
    {
        switch(hash)
        {
            case GTA.Native.Hash.GET_ASPECT_RATIO:return Aspect;
            case GTA.Native.Hash.GET_SAFE_ZONE_SIZE:return Safe;
            case GTA.Native.Hash.GET_DISABLED_CONTROL_NORMAL:return Convert.ToInt32(a[1])==239?MouseX:MouseY;
            case GTA.Native.Hash.IS_DISABLED_CONTROL_JUST_PRESSED:int control=Convert.ToInt32(a[1]);return control==237?Click:control==241?Wheel>0:control==242 && Wheel<0;
            case GTA.Native.Hash.HAS_STREAMED_TEXTURE_DICT_LOADED:return false;
            case GTA.Native.Hash.SET_TEXT_FONT:font=Convert.ToInt32(a[0]);break;
            case GTA.Native.Hash.SET_TEXT_SCALE:scale=Convert.ToSingle(a[1]);break;
            case GTA.Native.Hash.SET_TEXT_JUSTIFICATION:justify=Convert.ToInt32(a[0]);break;
            case GTA.Native.Hash.SET_TEXT_COLOUR:color=Color.FromArgb(Convert.ToInt32(a[3]),Convert.ToInt32(a[0]),Convert.ToInt32(a[1]),Convert.ToInt32(a[2]));break;
            case GTA.Native.Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT:case GTA.Native.Hash.BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT:text="";break;
            case GTA.Native.Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME:text+=Convert.ToString(a[0]);break;
            case GTA.Native.Hash.END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT:return Measure(text,scale);
            case GTA.Native.Hash.END_TEXT_COMMAND_DISPLAY_TEXT:
                float w=Measure(text,scale);Ops.Add(new NativeOp {Text=text,X=Convert.ToSingle(a[0])-(justify==2?w:0),Y=Convert.ToSingle(a[1]),W=w,H=scale*70/Height,Scale=scale,Color=color,Font=font});break;
            case GTA.Native.Hash.DRAW_RECT:
                float rw=Convert.ToSingle(a[2]),rh=Convert.ToSingle(a[3]);Ops.Add(new NativeOp {X=Convert.ToSingle(a[0])-rw/2,Y=Convert.ToSingle(a[1])-rh/2,W=rw,H=rh,Color=Color.FromArgb(Convert.ToInt32(a[7]),Convert.ToInt32(a[4]),Convert.ToInt32(a[5]),Convert.ToInt32(a[6]))});break;
        }
        return null;
    }
    public static void Save(string path)
    {
        using(var bmp=new Bitmap(Width,Height))using(var g=Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(35,45,41));g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAlias;
            foreach(var op in Ops)using(var brush=new SolidBrush(op.Color))
            {
                if(op.Text==null)g.FillRectangle(brush,op.X*Width,op.Y*Height,op.W*Width,op.H*Height);
                else using(var f=Font(op.Scale,op.Font))g.DrawString(op.Text,f,brush,op.X*Width,op.Y*Height,StringFormat.GenericTypographic);
            }
            var box=new HudLayout(Aspect,Safe);var crop=new Rectangle((int)(box.Left*Width)-10,(int)(box.Top*Height)-10,(int)(box.Width*Width)+20,(int)(box.Height*Height)+20);
            using(var panel=bmp.Clone(crop,PixelFormat.Format32bppArgb))panel.Save(path,ImageFormat.Png);
        }
    }
}
namespace GTA {public static class Game {public static int GameTime {get{return 1;}}}}
namespace GTA.UI
{
    public enum HudColor {Default=143}
    public static class HudColors {public static Color GetColor(HudColor color) {return Color.FromArgb(101,180,212);}}
    public static class Hud {public static void ShowCursorThisFrame() {}}
    public static class Notification {public static void PostTicker(string text,bool a,bool b) {}}
}
namespace GTA.Native
{
    public enum Hash {GET_ASPECT_RATIO,GET_SAFE_ZONE_SIZE,GET_DISABLED_CONTROL_NORMAL,IS_DISABLED_CONTROL_JUST_PRESSED,HIDE_HUD_COMPONENT_THIS_FRAME,HAS_STREAMED_TEXTURE_DICT_LOADED,REQUEST_STREAMED_TEXTURE_DICT,DRAW_SPRITE,SET_STREAMED_TEXTURE_DICT_AS_NO_LONGER_NEEDED,PLAY_SOUND_FRONTEND,DRAW_RECT,SET_TEXT_FONT,SET_TEXT_SCALE,SET_TEXT_JUSTIFICATION,SET_TEXT_WRAP,BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT,ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT,SET_TEXT_COLOUR,BEGIN_TEXT_COMMAND_DISPLAY_TEXT,END_TEXT_COMMAND_DISPLAY_TEXT}
    public static class Function
    {
        public static void Call(Hash hash,params object[] args) {NativeFrame.Call(hash,args);}
        public static T Call<T>(Hash hash,params object[] args) {object value=NativeFrame.Call(hash,args);return value==null?default(T):(T)value;}
    }
}
