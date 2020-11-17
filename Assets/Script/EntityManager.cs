#if !DISABLE_PLAYFABENTITY_API && !DISABLE_PLAYFABCLIENT_API

using PlayFab;
using PlayFab.Internal;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EntityManager : MonoBehaviour
{
    //543423a84f39d40782bffedcedc0ee4128e91e71
    private string myId = "543423a84f39d40782bffedcedc0ee4128e91e71";
    public string entityId, entityType; // Id representing the logged in player && entityType representing the logged in player
    private readonly Dictionary<string, string> _entityFileJson = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _tempUpdates = new Dictionary<string, string>();
    public string ActiveUploadFileName, NewFileName;
    // GlobalFileLock provides is a simplistic way to avoid file collisions, specifically designed for this example.
    public int GlobalFileLock = 0; 
    private bool firstLog = false, firstGet = false;
    private string keyName;

    private string keyBody, newKey, inputKey;
    private string[] bases = new string[] {"NGHH","MNOP","VWXY","FGHI"};
    [SerializeField]private List<string> keys = new List<string>();
    public Text inputBar;
    public GameObject wrongKeyAlert;

    // void start(){
        
    // }

    void OnSharedFailure(PlayFabError error){
        Debug.LogError(error.GenerateErrorReport());
        GlobalFileLock -= 1;
    }

    void OnGUI(){
        if(!PlayFabClientAPI.IsClientLoggedIn() && !firstLog){
            Login();
            firstLog = true;
        }
        else if(PlayFabClientAPI.IsClientLoggedIn() && !firstGet){
            LoadAllFiles();
            firstGet = true;
        }
        // if (!PlayFabClientAPI.IsClientLoggedIn() && GUI.Button(new Rect(0, 0, 100, 30), "Login"))
        //     Login();
        // if (PlayFabClientAPI.IsClientLoggedIn() && GUI.Button(new Rect(0, 0, 100, 30), "LogOut"))
        //     PlayFabClientAPI.ForgetAllCredentials();
        if (PlayFabClientAPI.IsClientLoggedIn() && GUI.Button(new Rect(100, 0, 100, 30), "(re)Load Files"))
            LoadAllFiles();

        if (PlayFabClientAPI.IsClientLoggedIn()){
            // Display existing files
            _tempUpdates.Clear();
            var index = 0;
            foreach (var each in _entityFileJson){
                GUI.Label(new Rect(100 * index, 60, 100, 30), each.Key);                
                var tempInput = _entityFileJson[each.Key];
                var tempOutput = GUI.TextField(new Rect(100 * index, 90, 100, 30), tempInput);
                if (tempInput != tempOutput)
                    _tempUpdates[each.Key] = tempOutput;
                // if (GUI.Button(new Rect(100 * index, 120, 100, 30), "Save " + each.Key))
                //     UploadFile(each.Key);
                if(GUI.Button(new Rect(100 * index, 120, 100, 30), "Delete " + each.Key))
                    DeleteFile(each.Key);
                index++;
            }
            // Apply any changes
            foreach (var each in _tempUpdates)
                _entityFileJson[each.Key] = each.Value;

            // Add a new file
            //NewFileName = GUI.TextField(new Rect(100 * index, 60, 100, 30), NewFileName);
            if (GUI.Button(new Rect(100 * index, 90, 100, 60), "Create new Key")){
                keyName = GenerateNewKey();
                UploadFile(keyName);
            }
        }
    }

    public string GenerateNewKey(){
        keyBody = (UnityEngine.Random.Range(1000, 9999)).ToString();
        newKey = bases[UnityEngine.Random.Range(0,4)] + keyBody;
        while(keys.Contains(newKey))   
        {
            keyBody = (UnityEngine.Random.Range(1000, 9999)).ToString();
            newKey = bases[UnityEngine.Random.Range(0,4)] + keyBody;
        }     
        keys.Add(newKey);
        print(newKey);
        return newKey;
    }

    void Login(){
        print(SystemInfo.deviceUniqueIdentifier);
        var request = new PlayFab.ClientModels.LoginWithCustomIDRequest{
            CustomId = myId,
            CreateAccount = true,
        };
        PlayFabClientAPI.LoginWithCustomID(request, OnLogin, OnSharedFailure);
    }

    void OnLogin(PlayFab.ClientModels.LoginResult result){
        entityId = result.EntityToken.Entity.Id;
        entityType = result.EntityToken.Entity.Type;
    }

    void LoadAllFiles(){
        if (GlobalFileLock != 0)
            throw new Exception("This example overly restricts file operations for safety. Careful consideration must be made when doing multiple file operations in parallel to avoid conflict.");

        GlobalFileLock += 1; // Start GetFiles
        keys.Clear();
        var request = new PlayFab.DataModels.GetFilesRequest { Entity = new PlayFab.DataModels.EntityKey { Id = entityId, Type = entityType } };
        PlayFabDataAPI.GetFiles(request, OnGetFileMeta, OnSharedFailure);
    }

    void OnGetFileMeta(PlayFab.DataModels.GetFilesResponse result){
        Debug.Log("Loading " + result.Metadata.Count + " files");

        _entityFileJson.Clear();
        foreach (var eachFilePair in result.Metadata)
        {
            _entityFileJson.Add(eachFilePair.Key, null);
            GetActualFile(eachFilePair.Value);
        }
        GlobalFileLock -= 1; // Finish GetFiles
    }

    void GetActualFile(PlayFab.DataModels.GetFileMetadata fileData){
        GlobalFileLock += 1; // Start Each SimpleGetCall
        PlayFabHttp.SimpleGetCall(fileData.DownloadUrl,
            result => { _entityFileJson[fileData.FileName] = Encoding.UTF8.GetString(result); keys.Add(fileData.FileName); GlobalFileLock -= 1; }, // Finish Each SimpleGetCall
            error => { Debug.Log(error); }
        );
    }

    void UploadFile(string fileName){
        if (GlobalFileLock != 0)
            throw new Exception("This example overly restricts file operations for safety. Careful consideration must be made when doing multiple file operations in parallel to avoid conflict.");

        ActiveUploadFileName = fileName;

        GlobalFileLock += 1; // Start InitiateFileUploads
        var request = new PlayFab.DataModels.InitiateFileUploadsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey { Id = entityId, Type = entityType },
            FileNames = new List<string> { ActiveUploadFileName },
        };
        PlayFabDataAPI.InitiateFileUploads(request, OnInitFileUpload, OnInitFailed);
    }

    void OnInitFailed(PlayFabError error){
        if (error.Error == PlayFabErrorCode.EntityFileOperationPending)
        {
            // This is an error you should handle when calling InitiateFileUploads, but your resolution path may vary
            GlobalFileLock += 1; // Start AbortFileUploads
            var request = new PlayFab.DataModels.AbortFileUploadsRequest
            {
                Entity = new PlayFab.DataModels.EntityKey { Id = entityId, Type = entityType },
                FileNames = new List<string> { ActiveUploadFileName },
            };
            PlayFabDataAPI.AbortFileUploads(request, (result) => { GlobalFileLock -= 1; UploadFile(ActiveUploadFileName); }, OnSharedFailure); GlobalFileLock -= 1; // Finish AbortFileUploads
            GlobalFileLock -= 1; // Failed InitiateFileUploads
        }
        else
            OnSharedFailure(error);
    }

    void OnInitFileUpload(PlayFab.DataModels.InitiateFileUploadsResponse response){
        string payloadStr;
        if (!_entityFileJson.TryGetValue(ActiveUploadFileName, out payloadStr))
            payloadStr = "{}";
        var payload = Encoding.UTF8.GetBytes(payloadStr);

        GlobalFileLock += 1; // Start SimplePutCall
        PlayFabHttp.SimplePutCall(response.UploadDetails[0].UploadUrl,
            payload,
            FinalizeUpload,
            error => { Debug.Log(error); }
        );
        GlobalFileLock -= 1; // Finish InitiateFileUploads
    }

    void FinalizeUpload(byte[] data){
        GlobalFileLock += 1; // Start FinalizeFileUploads
        var request = new PlayFab.DataModels.FinalizeFileUploadsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey { Id = entityId, Type = entityType },
            FileNames = new List<string> { ActiveUploadFileName },
        };
        PlayFabDataAPI.FinalizeFileUploads(request, OnUploadSuccess, OnSharedFailure);
        GlobalFileLock -= 1; // Finish SimplePutCall
    }

    void OnUploadSuccess(PlayFab.DataModels.FinalizeFileUploadsResponse result){
        Debug.Log("File upload success: " + ActiveUploadFileName);
        GlobalFileLock -= 1; // Finish FinalizeFileUploads
    }

    void DeleteFile(string file){
        GlobalFileLock += 1;
        var request = new PlayFab.DataModels.DeleteFilesRequest
        {
            Entity = new PlayFab.DataModels.EntityKey { Id = entityId, Type = entityType },
            FileNames = new List<string> { file },
        };
        PlayFabDataAPI.DeleteFiles(request, OnDeleteFilesSuccess, OnSharedFailure);
        keys.Remove(file);
    }

    void OnDeleteFilesSuccess(PlayFab.DataModels.DeleteFilesResponse result){
        print("deletou boe");
        GlobalFileLock -= 1;
    }

    public void UseKey(){
        inputKey = inputBar.text;
        if(keys.Contains(inputKey)){
            print("sucess");
            DeleteFile(inputKey);
            inputKey = "";
            inputBar.text = "";
        }
        else{
            if(wrongKeyAlert){
                wrongKeyAlert.SetActive(true);
                StartCoroutine("DisableAlert");
            }
        }            
    }

    public IEnumerator DisableAlert(){
        yield return new WaitForSeconds(2.5f);
        wrongKeyAlert.SetActive(false); 
    }
}

#endif
