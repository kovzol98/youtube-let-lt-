using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

class VideoLetolto : Form {
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 TextBox url=new TextBox(), folder=new TextBox(), log=new TextBox();
 Button paste,browse,start,cancel,open;
 Label status=new Label(); ProgressBar progress=new ProgressBar();
 Process running; bool stopping, listMode, hadErrors; string finalFile;
 CheckBox playlist=new CheckBox();
 int completed, itemIndex, itemTotal, expectedTotal, skipped; bool actualListSeen, rateLimited, busy;
 DateTime pauseUntil=DateTime.MinValue; string archivePath; Timer pauseTimer=new Timer();
 [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new VideoLetolto()); }
 VideoLetolto() {
  Text="Videóletöltő"; Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(247,249,252);
  ClientSize=new Size(800,660); MinimumSize=new Size(750,680); StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi;
  var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=12};
  foreach(int h in new int[]{46,32,26,44,34,26,44,56,28,30,26}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
  layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(layout);
  layout.Controls.Add(new Label {Text="Videóletöltő",Font=new Font("Segoe UI",21,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
  layout.Controls.Add(new Label {Text="Illeszd be a videó linkjét, és indítsd el a letöltést.",Dock=DockStyle.Fill,ForeColor=Color.DimGray},0,1);
  layout.Controls.Add(Caption("Videó linkje"),0,2);
  var links=Row(); url.Dock=DockStyle.Fill; url.AccessibleName="Videó linkje"; url.Margin=new Padding(0,4,10,4);
  paste=Btn("Beillesztés",delegate {try {if(Clipboard.ContainsText()) url.Text=Clipboard.GetText().Trim();} catch(Exception){status.Text="A vágólap most nem érhető el."; }});
  links.Controls.Add(url,0,0); links.Controls.Add(paste,1,0); layout.Controls.Add(links,0,3);
  layout.Controls.Add(Caption("Mentés helye"),0,5);
  var folders=Row(); folder.ReadOnly=true; folder.Dock=DockStyle.Fill; folder.Margin=new Padding(0,4,10,4); folder.AccessibleName="Mentés helye";
  folder.Text=Path.Combine(root,"Letoltott-videok");
  try {string saved=File.ReadAllText(Path.Combine(root,"mentesi-mappa.txt")).Trim(); if(Path.IsPathRooted(saved))folder.Text=saved;}catch(Exception){}
  browse=Btn("Tallózás…",delegate {using(var d=new FolderBrowserDialog {Description="Válaszd ki a letöltések helyét",SelectedPath=folder.Text}) if(d.ShowDialog(this)==DialogResult.OK){folder.Text=d.SelectedPath;try {File.WriteAllText(Path.Combine(root,"mentesi-mappa.txt"),folder.Text);}catch(Exception){}}});
  folders.Controls.Add(folder,0,0); folders.Controls.Add(browse,1,0); layout.Controls.Add(folders,0,6);
  var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,7,0,0)};
  start=Btn("Letöltés indítása",async delegate {await Download();}); start.Width=178; start.BackColor=Color.FromArgb(36,92,190); start.ForeColor=Color.White; start.FlatStyle=FlatStyle.Flat; start.FlatAppearance.BorderSize=0;
  cancel=Btn("Leállítás",delegate {Stop();}); cancel.Enabled=false; cancel.Width=108;
  open=Btn("Mappa megnyitása",delegate {try {Directory.CreateDirectory(folder.Text);Process.Start(new ProcessStartInfo(folder.Text){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"A mappa nem nyitható meg");}});open.Width=178;
  actions.Controls.AddRange(new Control[]{start,cancel,open});layout.Controls.Add(actions,0,7);
  progress.Dock=DockStyle.Fill;progress.Margin=new Padding(0,5,0,6);layout.Controls.Add(progress,0,8);
  status.Text="Készen áll a letöltésre.";status.Dock=DockStyle.Fill;status.AutoEllipsis=true;status.AccessibleName="Letöltés állapota";layout.Controls.Add(status,0,9);
  layout.Controls.Add(new Label {Text="MP4 előnyben • a listák külön mappába, számozott fájlokkal kerülnek",ForeColor=Color.DimGray,Font=new Font("Segoe UI",9),Dock=DockStyle.Fill},0,10);
  log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Dock=DockStyle.Fill;log.BackColor=Color.White;log.Font=new Font("Consolas",9);log.AccessibleName="Letöltési részletek";layout.Controls.Add(log,0,11);
  playlist.Text="Teljes lejátszási lista letöltése"; playlist.Dock=DockStyle.Fill; playlist.AccessibleName=playlist.Text; layout.Controls.Add(playlist,0,4);
  try { DateTime parsed;if(DateTime.TryParse(File.ReadAllText(Path.Combine(root,"youtube-piheno-utc.txt")),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed))pauseUntil=parsed.ToUniversalTime(); }catch(Exception){}
  try {var saved=File.ReadAllLines(Path.Combine(root,"utolso-link.txt"));if(saved.Length>0)url.Text=saved[0];if(saved.Length>1)playlist.Checked=saved[1]=="lista";}catch(Exception){}
  pauseTimer.Interval=1000;pauseTimer.Tick+=delegate{ApplyCooldown();};pauseTimer.Start();ApplyCooldown();
  FormClosed+=delegate{pauseTimer.Dispose();};
  AcceptButton=start;
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(running!=null&&!running.HasExited){if(MessageBox.Show(this,"Folyamatban van egy letöltés. Leállítod és bezárod a programot?","Kilépés",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){e.Cancel=true;return;}Stop();}};
 }
 Label Caption(string t){return new Label {Text=t,Dock=DockStyle.Fill,Font=new Font("Segoe UI",10,FontStyle.Bold)};}
 TableLayoutPanel Row(){var p=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));return p;}
 Button Btn(string t,EventHandler h){var b=new Button {Text=t,Size=new Size(118,36),Margin=new Padding(0,0,10,0),UseVisualStyleBackColor=true};b.Click+=h;return b;}
 // Encode individual arguments for Windows process creation; never invoke a shell.
 static string Quote(string s){var b=new StringBuilder("\"");int slashes=0;foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
 void UI(Action a){if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(a);}catch(InvalidOperationException){}}
 void Output(string line){if(String.IsNullOrWhiteSpace(line))return;UI(delegate{
  if(IsRateLimit(line)&&!rateLimited){PauseForLimit();}

  if(line.StartsWith("FILE:")){finalFile=line.Substring(5).Trim();if(File.Exists(finalFile)&&new FileInfo(finalFile).Length>0)completed++;return;}
  if(line.StartsWith("ITEM:")){var parts=line.Substring(5).Split('|');itemIndex=0;itemTotal=0;if(parts.Length>=2){Int32.TryParse(parts[0],out itemIndex);Int32.TryParse(parts[1],out itemTotal);}if(listMode&&parts.Length>=3&&itemIndex>0&&!String.IsNullOrWhiteSpace(parts[2])&&parts[2]!="NA")actualListSeen=true;if(expectedTotal>0)itemTotal=expectedTotal;progress.Value=0;progress.Style=ProgressBarStyle.Marquee;status.Text=ItemPrefix()+"Videó előkészítése…";return;}
  if(line.Contains("ERROR:")||(listMode&&line.Contains("Unable to recognize playlist")))hadErrors=true;
  log.AppendText(line+Environment.NewLine);
  if(log.TextLength>90000)log.Text=log.Text.Substring(log.TextLength-60000);
  if(line.Contains("Sleeping"))status.Text=ItemPrefix()+"Várakozás a következő kérés előtt…";
  var m=Regex.Match(line,@"\[download\]\s+(\d+(?:\.\d+)?)%");
  double n;if(m.Success&&Double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)){progress.Style=ProgressBarStyle.Continuous;progress.Value=Math.Max(0,Math.Min(100,(int)n));status.Text=ItemPrefix()+"Letöltés: "+m.Groups[1].Value+"%";}
 });}
 string ItemPrefix(){return listMode?(itemIndex>0?itemIndex+" / "+(itemTotal>0?itemTotal.ToString():"?")+". videó • ":"Lejátszási lista • "):"";}
 List<string> BuildArgs(string node,string address){
  var args=new List<string>{"--ignore-config",listMode?"--yes-playlist":"--no-playlist","--no-abort-on-error","--no-simulate","--newline","--no-colors","--encoding","utf-8","--progress","--socket-timeout","25","--retries","3","--cache-dir",Path.Combine(root,"cache"),"--js-runtimes","node:"+node,"--remote-components","ejs:github","-f","best[ext=mp4]/best","--windows-filenames","-P",folder.Text,"-o",listMode?"%(playlist_title,playlist_id|Lejatszasi-lista).100B/%(playlist_index)03d - %(title).150B [%(id)s].%(ext)s":"%(title).150B [%(id)s].%(ext)s","--print","before_dl:ITEM:%(playlist_index)s|%(n_entries)s|%(playlist_id)s","--print","after_move:FILE:%(filepath)s","--",address};
  if(!String.IsNullOrEmpty(archivePath))args.InsertRange(0,new string[]{"--download-archive",archivePath});
  args.InsertRange(0,new string[]{"-t","sleep","--sleep-requests","1","--sleep-interval","15","--max-sleep-interval","25"});
  return args;
 }
 static bool IsRateLimit(string line){
  string text=line.ToLowerInvariant();return text.Contains("rate-limited by youtube")||text.Contains("http error 429")||text.Contains("too many requests")||text.Contains("current session has been rate-limited");
 }
 void PauseForLimit(){
  rateLimited=true;hadErrors=true;pauseUntil=DateTime.UtcNow.AddHours(1);
  try{File.WriteAllText(Path.Combine(root,"youtube-piheno-utc.txt"),pauseUntil.ToString("o",CultureInfo.InvariantCulture));}catch(Exception e){log.AppendText(e.Message+Environment.NewLine);}
  status.Text="YouTube-korlátozás: a letöltés szünetel, a kész fájlok megmaradtak.";
  log.AppendText(Environment.NewLine+"A YouTube túl sok kérést jelzett. Leállítás; várakozás eddig: "+pauseUntil.ToLocalTime().ToString("HH:mm")+". A kész videók megmaradnak."+Environment.NewLine);
  try{if(running!=null&&!running.HasExited)running.Kill();}catch(InvalidOperationException){}
 }
 void ApplyCooldown(){
  if(busy)return;
  if(pauseUntil>DateTime.UtcNow){start.Enabled=false;start.Text="Várakozás";status.Text="YouTube-pihenő "+pauseUntil.ToLocalTime().ToString("HH:mm")+"-ig • hátra: "+(pauseUntil-DateTime.UtcNow).ToString(@"mm\:ss")+". Utána folytathatod.";}
  else if(pauseUntil!=DateTime.MinValue){pauseUntil=DateTime.MinValue;start.Enabled=true;start.Text="Letöltés folytatása";status.Text="A várakozás lejárt. A kész videók kihagyásával folytathatod.";}
 }
 HashSet<string> LocalVideoIds(){
  var ids=new HashSet<string>(StringComparer.Ordinal);
  if(!Directory.Exists(folder.Text))return ids;
  foreach(string file in Directory.EnumerateFiles(folder.Text,"*",SearchOption.AllDirectories)){
   var m=Regex.Match(Path.GetFileName(file),@" \[([A-Za-z0-9_-]{11})\]\.(mp4|webm|mkv)$",RegexOptions.IgnoreCase);
   if(m.Success&&new FileInfo(file).Length>0)ids.Add(m.Groups[1].Value);
  }
  return ids;
 }
 void PrepareResume(object[] entries){
  var ids=LocalVideoIds();Directory.CreateDirectory(folder.Text);archivePath=Path.Combine(folder.Text,"letoltesi-naplo.txt");
  var lines=new List<string>();foreach(string id in ids)lines.Add("youtube "+id);
  File.WriteAllLines(archivePath,lines.ToArray(),new UTF8Encoding(false));
  skipped=0;foreach(object entry in entries){var e=entry as Dictionary<string,object>;object id;if(e!=null&&e.TryGetValue("id",out id)&&ids.Contains(Convert.ToString(id)))skipped++;}
  completed=skipped;
  log.AppendText("Már mentve: "+skipped+" / "+expectedTotal+" videó; ezeket a program kihagyja."+Environment.NewLine);
 }
 static string NormalizeAddress(string address,bool wholeList){
  var u=new Uri(address);string host=u.Host.ToLowerInvariant();
  if(wholeList&&(host=="youtube.com"||host.EndsWith(".youtube.com")||host=="youtu.be")){
   string id=null;foreach(string part in u.Query.TrimStart('?').Split('&')){var pair=part.Split(new char[]{'='},2);if(pair.Length==2&&pair[0]=="list"){id=Uri.UnescapeDataString(pair[1]);break;}}
   if(String.IsNullOrWhiteSpace(id))throw new InvalidOperationException("A teljes listához lejátszásilista-link kell, amely tartalmazza a list= részt.");
   return "https://www.youtube.com/playlist?list="+Uri.EscapeDataString(id);
  }
  return u.AbsoluteUri;
 }
 async Task<int> RunEngine(string engine,List<string> args,Action<string> receive){
  var quoted=new List<string>();foreach(string a in args)quoted.Add(Quote(a));
  var process=new Process {StartInfo=new ProcessStartInfo {FileName=engine,Arguments=String.Join(" ",quoted.ToArray()),WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};
  running=process;
  try{
   process.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)receive(e.Data);};
   process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){Output(e.Data);};
   process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
   await Task.Run(delegate{process.WaitForExit();});await Task.Yield();return process.ExitCode;
  }finally{running=null;process.Dispose();}
 }
 async Task Download(){
  if(busy||running!=null)return;if(pauseUntil>DateTime.UtcNow){ApplyCooldown();return;}Uri address;
  if(!Uri.TryCreate(url.Text.Trim(),UriKind.Absolute,out address)||(address.Scheme!="https"&&address.Scheme!="http")){status.Text="Adj meg egy teljes videólinket (https://…).";url.Focus();return;}
  string engine=Path.Combine(root,"yt-dlp.exe"),node=Path.Combine(root,"node.exe");
  if(!File.Exists(engine)||!File.Exists(node)){MessageBox.Show(this,"Hiányzik a yt-dlp.exe vagy a node.exe. Tartsd a program fájljait egy mappában.","Hiányzó programfájl");return;}
  busy=true;rateLimited=false;archivePath=null;skipped=0;stopping=false;hadErrors=false;actualListSeen=false;completed=0;itemIndex=0;itemTotal=0;expectedTotal=0;listMode=playlist.Checked;finalFile=null;log.Clear();progress.Value=0;progress.Style=ProgressBarStyle.Marquee;
  start.Enabled=paste.Enabled=browse.Enabled=url.Enabled=playlist.Enabled=false;cancel.Enabled=true;
  try {
   string target=NormalizeAddress(address.AbsoluteUri,listMode);
   File.WriteAllLines(Path.Combine(root,"utolso-link.txt"),new string[]{url.Text.Trim(),listMode?"lista":"video"});
   if(listMode){
    status.Text="Lejátszási lista ellenőrzése és elemek számlálása…";
    log.AppendText("Lista ellenőrzése: "+target+Environment.NewLine);
    var json=new StringBuilder();
    int probeExit=await RunEngine(engine,new List<string>{"--ignore-config","--yes-playlist","--flat-playlist","--dump-single-json","--sleep-requests","1","--no-colors","--encoding","utf-8","--socket-timeout","25","--retries","2","--",target},delegate(string line){json.AppendLine(line);});
    if(rateLimited){return;}
    if(stopping){status.Text="Listaellenőrzés leállítva.";return;}
    if(probeExit!=0||hadErrors)throw new InvalidOperationException("A lejátszási lista nem olvasható. Nem indult el egyetlen videó letöltése sem. Ellenőrizd, hogy a lista nyilvános és a link helyes.");
    var serializer=new JavaScriptSerializer {MaxJsonLength=100000000};
    var info=serializer.DeserializeObject(json.ToString()) as Dictionary<string,object>;
    object kind,entriesValue,title;object[] entries=null;
    if(info==null||!info.TryGetValue("_type",out kind)||Convert.ToString(kind)!="playlist"||!info.TryGetValue("entries",out entriesValue)||(entries=entriesValue as object[])==null||entries.Length==0)throw new InvalidOperationException("Nem sikerült teljes lejátszási listát felismerni. Egyetlen videó letöltése helyett a művelet leállt.");
    expectedTotal=entries.Length;itemTotal=expectedTotal;PrepareResume(entries);
    string titleText=info.TryGetValue("title",out title)?Convert.ToString(title):"Lejátszási lista";
    log.AppendText("Felismert lista: "+titleText+" • "+expectedTotal+" elem"+Environment.NewLine);
    status.Text="Felismert lista: "+expectedTotal+" videó. Letöltés indítása…";
   }else status.Text="Videó adatainak lekérése…";
   if(listMode&&skipped==expectedTotal){progress.Style=ProgressBarStyle.Continuous;progress.Value=100;status.Text="A lista mind a "+expectedTotal+" videója már mentve van.";return;}
   Directory.CreateDirectory(folder.Text);
   int result=await RunEngine(engine,BuildArgs(node,target),Output);
   if(IsDisposed)return;
   progress.Style=ProgressBarStyle.Continuous;
   if(rateLimited){progress.Value=0;status.Text="YouTube-korlátozás: szünet. Megvan "+completed+" videó; később folytathatod.";}
   else if(stopping){status.Text="Leállítva. Elkészült: "+completed+" videó. Később folytathatod.";progress.Value=0;}
   else if(result==0&&!hadErrors&&completed>0&&(!listMode||(actualListSeen&&expectedTotal>0&&completed>=expectedTotal))){progress.Value=100;status.Text=listMode?"Lejátszási lista elkészült: "+completed+" / "+expectedTotal+" videó.":"Elkészült: "+Path.GetFileName(finalFile);log.AppendText(Environment.NewLine+"SIKERES LETÖLTÉS: "+(listMode?completed+" videó":finalFile)+Environment.NewLine);}
   else{progress.Value=0;status.Text=completed>0?"Részben kész: "+completed+(listMode?" / "+expectedTotal:"")+" videó. Részletek lent.":"A letöltés nem sikerült. A részleteket lent találod.";log.AppendText(Environment.NewLine+"A teljes letöltés nem igazolható. Elkészült: "+completed+" fájl."+Environment.NewLine);}
  }catch(Exception e){if(!IsDisposed){progress.Style=ProgressBarStyle.Continuous;progress.Value=0;status.Text="A letöltés nem sikerült. Részletek lent.";log.AppendText(e.Message+Environment.NewLine);}}
  finally{busy=false;if(!IsDisposed){progress.Style=ProgressBarStyle.Continuous;start.Enabled=paste.Enabled=browse.Enabled=url.Enabled=playlist.Enabled=true;cancel.Enabled=false;ApplyCooldown();}}
 }

 void Stop(){stopping=true;status.Text="Letöltés leállítása…";cancel.Enabled=false;try{if(running!=null&&!running.HasExited)running.Kill();}catch(Exception e){log.AppendText(e.Message+Environment.NewLine);}}
}


