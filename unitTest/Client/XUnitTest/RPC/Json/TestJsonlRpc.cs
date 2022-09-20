//------------------------------------------------------------------------------
//  此代码版权（除特别声明或在XREF结尾的命名空间的代码）归作者本人若汝棋茗所有
//  源代码使用协议遵循本仓库的开源协议及附加协议，若本仓库没有设置，则按MIT开源协议授权
//  CSDN博客：https://blog.csdn.net/qq_40374647
//  哔哩哔哩视频：https://space.bilibili.com/94253567
//  Gitee源代码仓库：https://gitee.com/RRQM_Home
//  Github源代码仓库：https://github.com/RRQM
//  API首页：https://www.yuque.com/rrqm/touchsocket/index
//  交流QQ群：234762506
//  感谢您的下载和使用
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
using TouchSocket.Core.Config;
using TouchSocket.Rpc.JsonRpc;
using TouchSocket.Sockets;
using Xunit;

namespace XUnitTest.RPC.Json
{
    public class TestJsonlRpc
    {
        [Theory]
        [InlineData("127.0.0.1:7803", JRPT.Tcp)]
        [InlineData("http://127.0.0.1:7801/jsonRpc", JRPT.Http)]
        public void ShouldSuccessfulCallService(string ipHost, JRPT jrpt)
        {
            var client = new TouchSocketConfig()
                 .SetJRPT(jrpt)
                 .SetRemoteIPHost(ipHost)
                 .BuildWithJsonRpcClient();

            RemoteTest remoteTest = new RemoteTest(client);
            remoteTest.Test01(null);
            remoteTest.Test02(null);
            remoteTest.Test03(null);
            remoteTest.Test04(null);
            remoteTest.Test05(null);

            remoteTest.Test09(null);
            remoteTest.Test10(null);
            remoteTest.Test11(null);
            remoteTest.Test12(null);
            remoteTest.Test13(null);
            remoteTest.Test14(null);
            remoteTest.Test15(null);
            remoteTest.Test16(null);
            remoteTest.Test17(null);
            remoteTest.Test18(null);
            remoteTest.Test22(null);
        }
    }
}