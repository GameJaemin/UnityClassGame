#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
public class ResultUiRunner : MonoBehaviour {
 static bool started;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Initialize() {
  if(!started && !Application.isEditor && Environment.GetCommandLineArgs().Contains("-soloSeriesTest")) { started=true; new GameObject("SoloSeriesTest").AddComponent<ResultUiRunner>(); }
 }
 static void Exit(int code) {
#if UNITY_EDITOR
  UnityEditor.EditorApplication.Exit(code);
#else
  Application.Quit(code);
#endif
 }
 string resultPath;
 void Fail(string reason) { File.WriteAllText(resultPath,"FAIL: "+reason); Exit(1); }
 IEnumerator Start() {
  DontDestroyOnLoad(gameObject);
  string[] args=Environment.GetCommandLineArgs();
  string output=args.SkipWhile(a=>a!="-smokeOutput").Skip(1).First();
  resultPath=Path.Combine(output,"solo-result.txt");
  yield return null;
  if(OnlineSeries.ResultsSeconds!=5 || !OnlineSeries.ContinueHint(5,2).Contains("5초 뒤")) { Fail("five second setting");yield break; }
  int initialWins=PlayerPrefs.GetInt("LionIceRun.Wins",0);
  PlayerPrefs.SetInt("LionIceRun.Skin",2); OnlineGameSession.Instance.frontend.characterSelection.Select(2);
  OnlineGameSession.Instance.frontend.soloButton.onClick.Invoke();
  float deadline=Time.realtimeSinceStartup+240;
  int forcedRound=0, observed=0;
  int[] skins=null;
  GameManager previousResult=null;
  float resultAt=0;
  int resultRound=0, timedTransitions=0;
  while(Time.realtimeSinceStartup<deadline) {
   var gm=GameManager.Instance;
   if(resultRound>0 && gm!=previousResult) {
    float delay=Time.realtimeSinceStartup-resultAt;
    if(delay<4.8f || delay>6f) { Fail("result transition delay="+delay); yield break; }
    timedTransitions++; resultRound=0;
    File.AppendAllText(Path.Combine(output,"solo-trace.txt"),"Result delay="+delay+"\n");
   }
   if(gm!=null && gm.State==GameManager.GameState.Playing && SoloGameSeries.Active) {
    var bodies=gm.Bodies();
    if(bodies.Count!=4) { Fail("roster count="+bodies.Count); yield break; }
    if(forcedRound!=SoloGameSeries.Round) {
     forcedRound=SoloGameSeries.Round; File.AppendAllText(Path.Combine(output,"solo-trace.txt"),"Playing round="+forcedRound+"\n");
     var currentSkins=bodies.Select(b=>(int)b.GetComponent<PlayerSkin>().skin).ToArray();
     if(skins==null) skins=currentSkins;
     else if(!skins.SequenceEqual(currentSkins)) { Fail("skins changed across scenes"); yield break; }
     if(currentSkins[0]!=2) { Fail("selected player skin lost"); yield break; }
     foreach(var brain in UnityEngine.Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled=false;
     int winner=(SoloGameSeries.Round-1)%4;
     for(int i=0;i<bodies.Count;i++) if(i!=winner) gm.WithdrawOnline(bodies[i]);
    }
   }
   if(gm!=null && gm.State==GameManager.GameState.GameOver && SoloGameSeries.Active) {
    yield return null;
    if(gm==null || gm!=GameManager.Instance || gm.State!=GameManager.GameState.GameOver || forcedRound!=SoloGameSeries.Round) continue;
    var front=OnlineGameSession.Instance.frontend;
    UnityEngine.Canvas.ForceUpdateCanvases();
    var rect=(RectTransform)front.resultLeaveButton.transform;
    var corners=new Vector3[4];rect.GetWorldCorners(corners);
    var canvas=(RectTransform)front.transform;
    var corner=canvas.InverseTransformPoint(corners[2]);
    if(Vector2.Distance((Vector2)corner,canvas.rect.max-new Vector2(24,24))>1
     ||rect.sizeDelta!=new Vector2(144,44)||Math.Abs(front.resultLeaveButton.GetComponent<CanvasGroup>().alpha-0.55f)>0.01f)
      { Fail("corner exit layout");yield break; }
    if(!SoloGameSeries.Complete) {
     if(!front.resultsHint.text.Contains("초 뒤 이어서 시작") || front.resultsHint.fontSizeMax<40)
      { Fail("continue emphasis");yield break; }
     if(resultRound==0 && previousResult!=gm) { previousResult=gm;resultAt=Time.realtimeSinceStartup;resultRound=SoloGameSeries.Round; }
    }
    observed=Math.Max(observed,SoloGameSeries.Round);
    if(!front.resultsPanel.activeSelf || front.resultText.text!=SoloGameSeries.ResultLines) { Fail("series results UI round="+SoloGameSeries.Round+", panel="+front.resultsPanel.activeSelf+", front="+front.isActiveAndEnabled+", session="+OnlineGameSession.Instance.isActiveAndEnabled+", actual="+front.resultText.text+", expected="+SoloGameSeries.ResultLines); yield break; }
    if(!SoloGameSeries.Complete && (front.returnButton.gameObject.activeSelf || PlayerPrefs.GetInt("LionIceRun.Wins",0)!=initialWins)) { Fail("early final button/win"); yield break; }
    if(SoloGameSeries.Complete) {
     int[] totals=SoloGameSeries.Scores.OrderBy(s=>s.actorId).Select(s=>s.total).ToArray();
     if(observed!=5 || timedTransitions!=4 || !totals.SequenceEqual(new[]{17,14,11,8}) || PlayerPrefs.GetInt("LionIceRun.Wins",0)!=initialWins+1) { Fail("final scores/win counter/transitions"); yield break; }
     string lines=SoloGameSeries.ResultLines;
     yield return new WaitForSecondsRealtime(9);
     if(!SoloGameSeries.Complete || SoloGameSeries.Round!=5) { Fail("unexpected sixth round"); yield break; }
     front.returnButton.onClick.Invoke();
     while(SoloGameSeries.Active || GameManager.Instance==gm) yield return null;
     yield return null;
     var title=OnlineGameSession.Instance;
     if(!title.roulette.titleGroup.activeSelf || title.roulette.rouletteGroup.activeSelf) { Fail("final title restoration"); yield break; }
     // 새 시리즈는 점수를 비우며, 중간 판의 나가기도 자동 진행을 취소한다.
     title.frontend.soloButton.onClick.Invoke();
     while(GameManager.Instance==null || GameManager.Instance.State!=GameManager.GameState.Playing) yield return null;
     if(SoloGameSeries.Round!=1 || SoloGameSeries.LocalTotal!=0) { Fail("new series scores not reset"); yield break; }
     OnlineGameSession.Instance.frontend.matchLeaveButton.onClick.Invoke();
     yield return new WaitForSecondsRealtime(2);
     if(SoloGameSeries.Active || !OnlineGameSession.Instance.roulette.titleGroup.activeSelf) { Fail("mid-series departure"); yield break; }
     File.WriteAllText(resultPath,"PASS: rounds=5, totals="+string.Join(",",totals)+", winsDelta=1\n"+lines+"PASS: stable skins, result UI, no sixth round, title return, fresh series, mid-series departure\n");
     Exit(0); yield break;
    }
   }
   yield return null;
  }
  Fail("timeout round="+SoloGameSeries.Round);
 }
}
#endif



