using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;
using agora_utilities;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class RtmpStreaming : MonoBehaviour
{
    [SerializeField] private string APP_ID = "";

    public string tokenGenerationURL = "http://3.128.168.232:8080/rtc/unity3d/publisher/uid/0/";
    [SerializeField] private string TOKEN = "";

    [SerializeField] private string CHANNEL_NAME = "YOUR_CHANNEL_NAME";

    [SerializeField] private string RTMP_URL = "";
    public string YouTube_RTMP_URL = "";
    public string Twitch_RTMP_URL = "";


    public Text logText;
    private Logger logger;
    private IRtcEngine mRtcEngine = null;
    private const float Offset = 100;
    private static string channelName = "Agora_Channel";
    private uint remoteUid = 0;
    private bool isStreaming = false;
    public int videoBitrate = 5000;
    public int videoWidth = 1920;
    public int videoHeight = 1080;
    public int videoFrameRate = 30;
    public FRAME_RATE videoEncoderFrameRate;
    public AUDIO_CODEC_PROFILE_TYPE audioCodecProfileType;
    public int audioBitrate = 128;
    public int audioChannels = 2;
    public AUDIO_SAMPLE_RATE_TYPE audioSampleRateType;
    public AUDIO_PROFILE_TYPE audioProfileType;
    public AUDIO_SCENARIO_TYPE audioScenarioType;
    public string audioDeviceStringToMatch = "VoiceMeeter";
    public string loopbackAudioDeviceName = "Speakers (3- Focusrite Usb Audio)";
    [Range(0, 100)]
    public int audioDeviceVolume = 0;
    public int _audioDeviceVolume = -1;
    [Range(0, 100)]
    public int recordingSignalVolume = 0;
    public int _recordingSignalVolume = -1;
    [Range(0, 100)]
    public int loopbackVolume = 100;
    public int _loopbackVolume = -1;
    bool updateVolumes = false;

    private AudioRecordingDeviceManager audioRecordingDeviceManager = null;
    private Dictionary<int, string> audioRecordingDeviceDict = new Dictionary<int, string>();



    // Use this for initialization
    void Start()
    {
        CheckAppId();
        InitEngine();
        JoinChannel();
    }

    // Update is called once per frame
    void Update()
    {
        PermissionHelper.RequestMicrophontPermission();
        PermissionHelper.RequestCameraPermission();
        if (updateVolumes)
        {
            if (audioDeviceVolume != _audioDeviceVolume)
            {
                audioRecordingDeviceManager.SetAudioRecordingDeviceVolume(audioDeviceVolume);
                var currentVol = audioRecordingDeviceManager.GetAudioRecordingDeviceVolume();
                Debug.Log("Current device vol = " + currentVol);
                _audioDeviceVolume = audioDeviceVolume;
            }
            if (loopbackVolume != _loopbackVolume)
            {
                mRtcEngine.AdjustLoopbackRecordingSignalVolume(loopbackVolume);
                _loopbackVolume = loopbackVolume;
            }
            if (recordingSignalVolume != _recordingSignalVolume)
            {
                mRtcEngine.AdjustRecordingSignalVolume(recordingSignalVolume);
                _recordingSignalVolume = recordingSignalVolume;
            }
        }
    }

    void CheckAppId()
    {
        logger = new Logger(logText);
        logger.DebugAssert(APP_ID.Length > 10, "Please fill in your appId in VideoCanvas!!!!!");
    }

    void InitEngine()
    {
        mRtcEngine = IRtcEngine.GetEngine(APP_ID);
        mRtcEngine.SetLogFile("log.txt");
        mRtcEngine.SetChannelProfile(CHANNEL_PROFILE.CHANNEL_PROFILE_LIVE_BROADCASTING);
        mRtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
        mRtcEngine.SetVideoEncoderConfiguration(new VideoEncoderConfiguration
        {
            dimensions = new VideoDimensions {width = videoWidth, height = videoHeight},
            frameRate = videoEncoderFrameRate
        });
        mRtcEngine.EnableAudio();
        mRtcEngine.EnableVideo();
        mRtcEngine.EnableVideoObserver();
        mRtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;
        mRtcEngine.OnLeaveChannel += OnLeaveChannelHandler;
        mRtcEngine.OnWarning += OnSDKWarningHandler;
        mRtcEngine.OnError += OnSDKErrorHandler;
        mRtcEngine.OnConnectionLost += OnConnectionLostHandler;
        mRtcEngine.OnUserJoined += OnUserJoinedHandler;
        mRtcEngine.OnUserOffline += OnUserOfflineHandler;
        mRtcEngine.OnStreamPublished += OnStreamPublishedHandler;
        mRtcEngine.OnRtmpStreamingStateChanged += OnRtmpStreamingStateChangedHandler;
        mRtcEngine.OnRtmpStreamingEvent += OnRtmpStreamingEventHandler;
    }

    void StartTranscoding(bool ifRemoteUser = false)
    {
        if (isStreaming && !ifRemoteUser) return;
        if (isStreaming && ifRemoteUser)
        {
            mRtcEngine.RemovePublishStreamUrl(RTMP_URL);
        }
        
        var lt = new LiveTranscoding();
        lt.videoBitrate = videoBitrate;
        lt.videoCodecProfile = VIDEO_CODEC_PROFILE_TYPE.VIDEO_CODEC_PROFILE_HIGH;
        lt.videoGop = 30;
        lt.videoFramerate = videoFrameRate;
        lt.lowLatency = false;
        lt.audioSampleRate = audioSampleRateType;
        lt.audioBitrate = audioBitrate;
        lt.audioChannels = audioChannels;
        lt.audioCodecProfile = audioCodecProfileType;
        lt.liveStreamAdvancedFeatures = new LiveStreamAdvancedFeature[0];
        
        mRtcEngine.SetAudioProfile(audioProfileType, audioScenarioType);

        var localUser = new TranscodingUser()
        {
            uid = 0,
            x = 0,
            y = 0,
            width = videoWidth,
            height = videoHeight,
            audioChannel = 0,
            alpha = 1.0,
        };
        
        if (ifRemoteUser)
        {
            var remoteUser = new TranscodingUser()
            {
                uid = remoteUid,
                x = 360,
                y = 0,
                width = videoWidth,
                height = videoHeight,
                audioChannel = 0,
                alpha = 1.0,
            };
            lt.userCount = 2;
            lt.width = videoWidth;
            lt.height = videoHeight;
            lt.transcodingUsers = new[] {localUser, remoteUser};
        }
        else
        {
            lt.userCount = 1;
            lt.width = videoWidth;
            lt.height = videoHeight;
            lt.transcodingUsers = new[] {localUser};
        }
        
        mRtcEngine.SetLiveTranscoding(lt);

        var rc = mRtcEngine.AddPublishStreamUrl(RTMP_URL, true);
        if (rc == 0) logger.UpdateLog(string.Format("Error in AddPublishStreamUrl: {0}", RTMP_URL));
    }

    void GetAudioRecordingDevice()
    {
        string audioRecordingDeviceName = "";
        string audioRecordingDeviceId = "";
        audioRecordingDeviceManager = (AudioRecordingDeviceManager)mRtcEngine.GetAudioRecordingDeviceManager();
        audioRecordingDeviceManager.CreateAAudioRecordingDeviceManager();
        int count = audioRecordingDeviceManager.GetAudioRecordingDeviceCount();
        logger.UpdateLog(string.Format("AudioRecordingDevice count: {0}", count));
        for (int i = 0; i < count; i++)
        {
            audioRecordingDeviceManager.GetAudioRecordingDevice(i, ref audioRecordingDeviceName, ref audioRecordingDeviceId);
            audioRecordingDeviceDict.Add(i, audioRecordingDeviceId);
            //logger.UpdateLog(string.Format("----AudioRecordingDevice device index: {0}, name: {1}, id: {2}", i, audioRecordingDeviceName, audioRecordingDeviceId));
            Debug.Log(string.Format("----AudioRecordingDevice device index: {0}, name: {1}, id: {2}", i, audioRecordingDeviceName, audioRecordingDeviceId));
            if (audioRecordingDeviceName.Contains(audioDeviceStringToMatch))
            {
                var setAudioRecordingDevice = audioRecordingDeviceManager.SetAudioRecordingDevice(audioRecordingDeviceDict[i]);
                Debug.Log(" *** device selected: " + audioRecordingDeviceName + " // " + audioRecordingDeviceDict[i] + ". Return code = " + setAudioRecordingDevice);
                audioRecordingDeviceManager.SetAudioRecordingDeviceVolume(audioDeviceVolume);
                break;
            }
        }
    }

    void JoinChannel()
    {
        mRtcEngine.JoinChannelByKey(TOKEN, CHANNEL_NAME, "", 0);
    }

    void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        logger.UpdateLog(string.Format("sdk version: ${0}", IRtcEngine.GetSdkVersion()));
        logger.UpdateLog(string.Format("onJoinChannelSuccess channelName: {0}, uid: {1}, elapsed: {2}", channelName,
            uid, elapsed));
        makeVideoView(0);

        GetAudioRecordingDevice();

        var resultLoopback = mRtcEngine.EnableLoopbackRecording(true, null);
        var resultLoopbackVolume = mRtcEngine.AdjustLoopbackRecordingSignalVolume(loopbackVolume);
        Debug.Log($" --*- LOOPBACK {resultLoopback} // Volume: {resultLoopbackVolume}");

        // GetAudioRecordingDeviceName();

        StartTranscoding();

        updateVolumes = true;
    }

    void OnLeaveChannelHandler(RtcStats stats)
    {
        logger.UpdateLog("OnLeaveChannelSuccess");
        DestroyVideoView(0);
    }

    void OnUserJoinedHandler(uint uid, int elapsed)
    {
        if (remoteUid == 0) remoteUid = uid;
        StartTranscoding(true);
        logger.UpdateLog(string.Format("OnUserJoined uid: ${0} elapsed: ${1}", uid, elapsed));
        makeVideoView(uid);
    }

    void OnUserOfflineHandler(uint uid, USER_OFFLINE_REASON reason)
    {
        remoteUid = 0;
        logger.UpdateLog(string.Format("OnUserOffLine uid: ${0}, reason: ${1}", uid, (int) reason));
        DestroyVideoView(uid);
    }

    void OnSDKWarningHandler(int warn, string msg)
    {
        logger.UpdateLog(string.Format("OnSDKWarning warn: {0}, msg: {1}", warn, msg));
    }

    void OnSDKErrorHandler(int error, string msg)
    {
        logger.UpdateLog(string.Format("OnSDKError error: {0}, msg: {1}", error, msg));
    }

    void OnConnectionLostHandler()
    {
        logger.UpdateLog("OnConnectionLost ");
    }

    void OnStreamPublishedHandler(string url, int error)
    {
        logger.UpdateLog(string.Format("OnStreamPublished url: {0}, error : {1}", url, error));
    }

    void OnRtmpStreamingStateChangedHandler(string url, RTMP_STREAM_PUBLISH_STATE state, RTMP_STREAM_PUBLISH_ERROR_TYPE code)
    {
        logger.UpdateLog(string.Format("OnRtmpStreamingStateChanged url: {0}, state: {1}, code: {2}", url, state,
            code));
    }

    void OnRtmpStreamingEventHandler(string url, RTMP_STREAMING_EVENT code)
    {
        logger.UpdateLog(string.Format("OnRtmpStreamingEvent url: {0}, code: {1}", url, code));
    }

    void OnApplicationQuit()
    {
        Debug.Log("OnApplicationQuit");
        if (mRtcEngine != null)
        {
            mRtcEngine.RemovePublishStreamUrl(RTMP_URL);
            mRtcEngine.LeaveChannel();
            mRtcEngine.DisableVideoObserver();
            IRtcEngine.Destroy();
        }
    }

    private void DestroyVideoView(uint uid)
    {
        GameObject go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            Object.Destroy(go);
        }
    }

    private void makeVideoView(uint uid)
    {
        GameObject go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            return; // reuse
        }

        // create a GameObject and assign to this new user
        VideoSurface videoSurface = makeImageSurface(uid.ToString());
        if (!ReferenceEquals(videoSurface, null))
        {
            // configure videoSurface
            videoSurface.SetForUser(uid);
            videoSurface.SetEnable(true);
            videoSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            videoSurface.SetGameFps(30);
        }
    }

    // VIDEO TYPE 1: 3D Object
    public VideoSurface makePlaneSurface(string goName)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);

        if (go == null)
        {
            return null;
        }

        go.name = goName;
        // set up transform
        go.transform.Rotate(-90.0f, 0.0f, 0.0f);
        float yPos = Random.Range(3.0f, 5.0f);
        float xPos = Random.Range(-2.0f, 2.0f);
        go.transform.position = new Vector3(xPos, yPos, 0f);
        go.transform.localScale = new Vector3(0.25f, 0.5f, .5f);

        // configure videoSurface
        VideoSurface videoSurface = go.AddComponent<VideoSurface>();
        return videoSurface;
    }

    // Video TYPE 2: RawImage
    public VideoSurface makeImageSurface(string goName)
    {
        GameObject go = new GameObject();

        if (go == null)
        {
            return null;
        }

        go.name = goName;
        // to be renderered onto
        go.AddComponent<RawImage>();
        // make the object draggable
        go.AddComponent<UIElementDrag>();
        GameObject canvas = GameObject.Find("VideoCanvas");
        if (canvas != null)
        {
            go.transform.parent = canvas.transform;
            Debug.Log("add video view");
        }
        else
        {
            Debug.Log("Canvas is null video view");
        }

        // set up transform
        go.transform.Rotate(0f, 0.0f, 180.0f);
        float xPos = Random.Range(Offset - Screen.width / 2f, Screen.width / 2f - Offset);
        float yPos = Random.Range(Offset, Screen.height / 2f - Offset);
        Debug.Log("position x " + xPos + " y: " + yPos);
        go.transform.localPosition = new Vector3(xPos, yPos, 0f);
        go.transform.localScale = new Vector3(3.555555f, 2f, 1f);

        // configure videoSurface
        VideoSurface videoSurface = go.AddComponent<VideoSurface>();
        return videoSurface;
    }
}