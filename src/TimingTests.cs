using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class TimingTests {
 static Block B(string kind,params string[] values){return Block.Make(kind,values);}
 static void Check(bool condition,string message){if(!condition)throw new Exception("Odstępy: "+message);}
 static void Healthy(RuntimeView runtime){Check(runtime.Errors.Count==0,string.Join("; ",runtime.Errors));}
 static async Task Within(Task task,int milliseconds,string message){
  using(CancellationTokenSource timeout=new CancellationTokenSource()){
   Task winner=await Task.WhenAny(task,Task.Delay(milliseconds,timeout.Token));
   if(winner!=task)throw new Exception("Odstępy: przekroczono czas: "+message);
   timeout.Cancel();await task;
  }
 }
 static async Task Until(Func<bool> predicate,int milliseconds,string message){
  Stopwatch watch=Stopwatch.StartNew();
  while(!predicate()){
   if(watch.ElapsedMilliseconds>milliseconds)throw new Exception("Odstępy: przekroczono czas: "+message);
   await Task.Delay(10);
  }
 }
 static bool Has(RuntimeView runtime,string key){return runtime.Values.ContainsKey(key);}
 static double Number(RuntimeView runtime,string key){return RuntimeView.Number(runtime.Get(key));}

 public static async Task Run(RuntimeView runtime,List<string> results){
  bool savedSilent=runtime.Silent;Action<Block> savedHighlight=runtime.Highlight;
  Task running=null;Exception failure=null;
  try{
   Project roundTrip=new Project{StepDelaySeconds=.25};roundTrip.Blocks.Add(B("set","n","1"));
   Check(ProjectIO.FromJson(ProjectIO.Json(roundTrip)).StepDelaySeconds==.25,"serializacja nie zachowała odstępu");
   Check(ProjectIO.Parse(ProjectIO.Export(roundTrip,Application.ExecutablePath)).StepDelaySeconds==.25,"wyeksportowany .js nie zachował odstępu");
   Project legacy=ProjectIO.FromJson("{\"Version\":2,\"Name\":\"legacy\",\"Blocks\":[]}");
   Check(legacy.StepDelaySeconds==0,"stary projekt nie dostał domyślnego odstępu 0");
   foreach(double value in new[]{-.01,3600.01,double.NaN,double.PositiveInfinity,double.NegativeInfinity}){
    bool rejected=false;try{ProjectIO.Normalize(new Project{StepDelaySeconds=value});}catch(Exception){rejected=true;}
    Check(rejected,"przyjęto nieprawidłowy odstęp "+value);
   }
   ProjectIO.Normalize(new Project{StepDelaySeconds=0});ProjectIO.Normalize(new Project{StepDelaySeconds=3600});
   results.Add("PASS odstępy: zapis i eksport .js, zgodność ze starszymi projektami oraz walidacja 0–3600 s");

   runtime.Silent=false;
   Stopwatch clock=new Stopwatch();List<Tuple<Block,long>> trace=new List<Tuple<Block,long>>();
   runtime.Highlight=delegate(Block block){if(block!=null)trace.Add(Tuple.Create(block,clock.ElapsedMilliseconds));};

   // First and last blocks are observed independently of the total running time.
   // A delay before the first action or after the last action must be detected.
   Project pair=new Project{StepDelaySeconds=.18};Block first=B("set","first","1"),second=B("set","second","1");
   pair.Blocks.AddRange(new[]{first,second});clock.Restart();trace.Clear();running=runtime.Start(pair);
   Check(Has(runtime,"first")&&!Has(runtime,"second"),"pierwszy bloczek nie wykonał się od razu albo drugi wykonał się bez odstępu");
   Check(!running.IsCompleted,"program nie czekał przed drugim bloczkiem");
   await Within(running,1000,"dwa bloczki");Healthy(runtime);
   long gap=trace.Single(t=>t.Item1==second).Item2-trace.Single(t=>t.Item1==first).Item2;
   Check(gap>=145,"rzeczywisty odstęp był za krótki: "+gap+" ms");
   Check(clock.ElapsedMilliseconds-trace.Single(t=>t.Item1==second).Item2<130,"dodano zbędną pauzę po ostatnim bloczku");
   Check(Number(runtime,"second")==1,"drugi bloczek nie wykonał się");
   results.Add("PASS odstępy: rzeczywista pauza między akcjami, bez pauzy początkowej i końcowej");

   Project nested=new Project{StepDelaySeconds=.12};Block initial=B("set","n","0");nested.Blocks.Add(initial);
   nested.Blocks.Add(B("comment","Komentarz nie opóźnia"));Block disabled=B("set","disabled","1");disabled.Disabled=true;nested.Blocks.Add(disabled);
   Block repeat=B("repeat","2"),condition=B("if","1","1"),increment=B("change","n","1");
   condition.Op="==";condition.Children.Add(increment);repeat.Children.Add(condition);nested.Blocks.Add(repeat);
   Block flag=B("event_start"),last=B("set","finished","1");flag.Children.Add(last);nested.Blocks.Add(flag);
   clock.Restart();trace.Clear();running=runtime.Start(nested);await Within(running,1600,"zagnieżdżone skrypty");Healthy(runtime);
   List<long> actionTimes=trace.Where(t=>t.Item1==initial||t.Item1==increment||t.Item1==last).Select(t=>t.Item2).ToList();
   Check(actionTimes.Count==4&&Number(runtime,"n")==2&&Number(runtime,"finished")==1,"nieprawidłowy wynik pętli lub zdarzenia flagi");
   Check(!Has(runtime,"disabled"),"wyłączony bloczek został wykonany");
   for(int i=1;i<actionTimes.Count;i++){
    long elapsed=actionTimes[i]-actionTimes[i-1];
    Check(elapsed>=90&&elapsed<220,"pauza w kontenerze została pominięta albo zdublowana: "+elapsed+" ms");
   }
   results.Add("PASS odstępy: ciągłość przez pętle i kontenery; komentarze oraz wyłączone bloczki nie dodają pauz");

   Project events=new Project{StepDelaySeconds=.12};events.Blocks.Add(B("set","root","1"));
   Block key=B("event_key","Space");key.Children.Add(B("set","event_first","1"));key.Children.Add(B("set","event_second","1"));events.Blocks.Add(key);
   running=runtime.Start(events);await Within(running,600,"inicjalizacja zdarzenia");Healthy(runtime);
   runtime.FeedKey(Keys.Space,true);runtime.FeedKey(Keys.Space,false);
   Check(Has(runtime,"event_first")&&!Has(runtime,"event_second"),"nowe zdarzenie odziedziczyło pauzę albo nie zachowało własnego odstępu");
   await Until(()=>Has(runtime,"event_second"),700,"drugi bloczek zdarzenia");Healthy(runtime);runtime.Stop();
   results.Add("PASS odstępy: każde nowe zdarzenie zaczyna od razu i zachowuje własną kolejność");

   Project explicitWait=new Project{StepDelaySeconds=.04};Block before=B("set","before","1"),wait=B("wait","0.08"),after=B("set","after","1");
   explicitWait.Blocks.AddRange(new[]{before,wait,after});clock.Restart();trace.Clear();running=runtime.Start(explicitWait);
   await Within(running,900,"jawny bloczek Czekaj");Healthy(runtime);
   long afterWait=trace.Single(t=>t.Item1==after).Item2-trace.Single(t=>t.Item1==wait).Item2;
   Check(afterWait>=95,"odstęp nie jest liczony po zakończeniu bloczka Czekaj: "+afterWait+" ms");
   Check(clock.ElapsedMilliseconds>=130,"jawny Czekaj nie dodał swojego czasu");
   results.Add("PASS odstępy: Czekaj dodaje własny czas, a kolejna pauza rozpoczyna się po jego zakończeniu");

   Project stop=new Project{StepDelaySeconds=1.25};stop.Blocks.Add(B("set","started","1"));stop.Blocks.Add(B("set","must_not_run","1"));
   running=runtime.Start(stop);Check(Has(runtime,"started"),"pierwsza akcja przed Stop nie wykonała się");
   await Task.Delay(35);clock.Restart();runtime.Stop();await Within(running,450,"Stop podczas odstępu");
   Check(clock.ElapsedMilliseconds<450&&!Has(runtime,"must_not_run"),"Stop czekał na zakończenie odstępu lub dopuścił kolejną akcję");Healthy(runtime);
   results.Add("PASS odstępy: Stop natychmiast anuluje oczekiwanie i nie wykonuje kolejnego bloczka");
  }
  catch(Exception ex){failure=ex;}
  finally{runtime.Stop();runtime.Silent=savedSilent;runtime.Highlight=savedHighlight;}

  // Keep cleanup outside finally for compatibility with the C# 5 compiler.
  if(running!=null&&!running.IsCompleted){
   try{await Within(running,1800,"sprzątanie anulowanego skryptu");}
   catch(Exception ex){if(failure==null)failure=ex;}
  }
  await Task.Delay(20); // Allow cancelled async event handlers to leave their finally blocks.
  if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
 }
}
}
