
using com.unity.mgobe.src.Net;
using com.unity.mgobe.src.Net.Sockets;
using com.unity.mgobe.src.Ping;
using com.unity.mgobe.src.Sender;
using com.unity.mgobe.src.User;
using com.unity.mgobe.src.Util;
using com.unity.mgobe.src.Util.Def;

// using Minigame.SdkType;

namespace com.unity.mgobe.src
{
    public static class Core
    {
        public static Socket Socket1 { get; set; } = null;

        public static Socket Socket2 { get; set; } = null;

        public static Pinger Pinger1 { get; set; } = null;

        public static Pinger Pinger2 { get; set; } = null;

        public static User.User User { get; set; } = null;

        public static Matcher.Matcher Matcher { get; set; } = null;

        public static Room.Room Room { get; set; } = null;

        public static Sender.Sender Sender { get; set; } = null;

        public static FrameSender FrameSender { get; set; } = null;

        private static void InitModules()
        {
            Core.User = new User.User(Sdk.Responses);
            Core.Matcher = new Matcher.Matcher(Sdk.Responses);
            Core.Sender = new Sender.Sender(Sdk.Responses);
            Core.Room = new Room.Room(Sdk.Responses);
            Core.FrameSender = new FrameSender(Sdk.Responses);

            Core.Socket1 = new Socket(0, false, null);
            Core.Socket2 = new Socket(1, true, null);

            Core.Pinger1 = new Pinger(Sdk.Responses, 0, null);
            Core.Pinger2 = new Pinger(Sdk.Responses, 1, FrameSender);

            var route1 = new BaseNetUtil[6] { User, Room, Sender, FrameSender.NetUtil1, Pinger1, Matcher };
            var route2 = new BaseNetUtil[2] { FrameSender.NetUtil2, Pinger2 };

            foreach (var request in route1)
            {
                request.BindSocket(Core.Socket1);
            }
            foreach (var request in route2)
            {
                request.BindSocket(Core.Socket2);
            }

            Sdk.UpdateSdk();
        }

        private static void UnInitModules()
        {
            Socket1?.DestroySocketTask();
            Socket2?.DestroySocketTask();
            var route = new BaseNetUtil[8] { User, Room, Sender, Matcher, FrameSender.NetUtil1, FrameSender.NetUtil2, Pinger1, Pinger2 };
            foreach (var request in route)
            {
                request?.UnbindSocket();
            }
        }

        public static void InitSdk()
        {
            if (!SdkStatus.IsUnInit()) return;
            if (User != null) UnInitSdk();
            // ???????????????
            SdkStatus.SetStatus(SdkStatus.StatusType.Initing);

            Core.InitModules();
            BaseNetUtil.StopQueueLoop();
            BaseNetUtil.StartQueueLoop();

            // ?????? Socket ????????????
            Socket1.Url = Config.Url;

            // loginEvent += onSocketConnect;
            ListenSocketConnect();
            
            Socket1.ConnectSocketTask("init Sdk");
        }

        public static void UnInitSdk()
        {
            Sdk.Instance.ClearResponse();
            Core.UnInitModules();
            SdkStatus.SetStatus(SdkStatus.StatusType.Uninit);
            UserStatus.SetStatus(UserStatus.StatusType.Logout);
            // Sdk.Uninit();
        }

        private static void ListenSocketConnect()
        {
            // ??????
            Socket1.OnEvent("connect", (SocketEvent socketEvent) =>
            {
                // ???????????????Login 
                if (!UserStatus.IsStatus(UserStatus.StatusType.Logining))
                {
                    UserUtil.Login(null);
                }

                if (string.IsNullOrEmpty(Socket1.Url)) return;
                var eve = new ResponseEvent(ErrCode.EcOk) {Data = Socket1.Id};
                Sdk.Responses.OnNetwork(eve);
            });
            Socket2.OnEvent("connect", (SocketEvent socketEvent) =>
            {   
                // check login ???????????????????????????
                FrameSender.CheckLogin(null, "connect " + !!Socket2.IsSocketStatus("connect"));
                if (!string.IsNullOrEmpty(Socket2.Url))
                {
                    var eve = new ResponseEvent(ErrCode.EcOk) {Data = Socket2.Id};
                    Sdk.Responses.OnNetwork(eve);
                }
                Pinger2.Ping(null);
            });

            // ??????
            Socket1.OnEvent("connectClose", (SocketEvent socketEvent) =>
            {
                // ???????????????
                SdkInitCallback(false, new ResponseEvent(ErrCode.EcSdkSocketClose));
                if (!SdkStatus.IsInited()) { return; }
                // ??????????????? Logout
                UserStatus.SetStatus(UserStatus.StatusType.Logout);
                if (string.IsNullOrEmpty(Socket1.Url)) return;
                var eve = new ResponseEvent(ErrCode.EcSdkSocketClose, "Socket ??????", null, null);
                Sdk.Responses.OnNetwork(eve);
            });
            Socket2.OnEvent("connectClose", (SocketEvent socketEvent) =>
            {
                if (!SdkStatus.IsInited()) { return; }
                Debugger.Log("socket2 on connect close");
                CheckLoginStatus.SetStatus(CheckLoginStatus.StatusType.Offline);
                if (!string.IsNullOrEmpty(Socket2.Url))
                {
                    var eve = new ResponseEvent(ErrCode.EcSdkSocketClose, "Socket ??????", null, null);
                    Sdk.Responses.OnNetwork(eve);
                };
                Pinger2.Stop();
            });

            // socket ??????
            Socket1.OnEvent("connectError", (SocketEvent socketEvent) =>
            {
                // ???????????????
                SdkInitCallback(false, new ResponseEvent(ErrCode.EcSdkSocketError));
                if (!SdkStatus.IsInited()) return;
                if (string.IsNullOrEmpty(Socket1.Url)) return;
                var eve = new ResponseEvent(ErrCode.EcSdkSocketError, "Socket ??????", null, null);
                Sdk.Responses.OnNetwork(eve);
            });
            Socket2.OnEvent("connectError", (SocketEvent socketEvent) =>
            {
                if (!SdkStatus.IsInited()) return;
                if (string.IsNullOrEmpty(Socket2.Url)) return;
                var eve = new ResponseEvent(ErrCode.EcSdkSocketError, "Socket ??????", null, null);
                Sdk.Responses.OnNetwork(eve);
            });

            // ??????????????????
            Socket1.OnEvent("autoAuth", (SocketEvent socketEvent) =>
            {
                if (!SdkStatus.IsInited()) return;
                var timer = new Timer();
                timer.SetTimeout(() =>
                {
                    var isLogout = UserStatus.IsStatus(UserStatus.StatusType.Logout);
                    if (!string.IsNullOrEmpty(Socket1.Url) && isLogout)
                    {
                        UserUtil.Login(null);
                    };
                }, 1000);
            });
            Socket2.OnEvent("autoAuth", (SocketEvent socketEvent) =>
            {
                if (!SdkStatus.IsInited()) return;
                if (string.IsNullOrEmpty(Socket2.Url)) return;
                var timer = new Timer();
                timer.SetTimeout(() =>
                {
                    // Debugger.Log("auto auth check 1");
                    // ???????????????????????????
                    if (UserStatus.IsStatus(UserStatus.StatusType.Logout)) UserUtil.Login(null);

                    // ?????????????????? checkLogin
                    var info = FrameSender.RoomInfo ?? new RoomInfo { RouteId = "" };
                    // Debugger.Log("auto auth check 2: {0}", CheckLoginStatus.GetRouteId() != info.RouteId);

                    if (CheckLoginStatus.IsOffline() || CheckLoginStatus.GetRouteId() != info.RouteId)
                    {
                        FrameSender.CheckLogin((ResponseEvent eve) =>
                        {
                            if (eve.Code == ErrCode.EcOk)
                            {
                                Pinger2.Ping(null);
                            }
                        }, "autoAuth");
                    }
                }, 1000);
            });
        }

        // ?????????????????????
        public static void SdkInitCallback(bool success, ResponseEvent eve)
        {
            // ??????Sdk???:
            if (!SdkStatus.IsIniting()) return;
            // ???????????????
            if (success) SdkStatus.SetStatus(SdkStatus.StatusType.Inited);

            //  ???????????????
            if (!success) SdkStatus.SetStatus(SdkStatus.StatusType.Uninit);

            // ??????
            var code = SdkStatus.IsInited() ? ErrCode.EcOk : ErrCode.EcSdkUninit;
            if (!success && eve != null && eve.Code != ErrCode.EcOk)
            {
                code = eve.Code;
            }

            // ????????????
            var msg = SdkStatus.IsInited() ? "???????????????" : "???????????????";
                
            // ??????????????????
            var initRsp = (InitRsp)eve?.Data ?? null;
            ulong serverTime = initRsp?.ServerTime ?? 0;

            var e = new ResponseEvent(code, msg, null, new InitRsp(serverTime));
            
            Sdk.Instance.InitRsp(e);
            if(!SdkStatus.IsInited()) Sdk.Uninit(); 
        }
    }
}