﻿using System;
using TouchSocket.Core;

namespace TouchSocket.Rpc.TouchRpc
{
    /// <summary>
    /// RedisPlugin
    /// </summary>
    public class RedisPlugin : TouchRpcPluginBase<IDependencyTouchRpc>
    {
        /// <summary>
        /// 定义元素的序列化和反序列化。
        /// <para>注意：Byte[]类型不用考虑。内部单独会做处理。</para>
        /// </summary>
        public BytesConverter Converter { get; private set; } = new BytesConverter();

        /// <summary>
        /// 实际储存缓存。
        /// </summary>
        public ICache<string, byte[]> ICache { get; set; } = new MemoryCache<string, byte[]>();

        /// <summary>
        /// 设置实际储存缓存。
        /// </summary>
        /// <param name="cache"></param>
        public void SetCache(ICache<string, byte[]> cache)
        {
            ICache = cache;
        }

        /// <summary>
        /// 定义元素的序列化和反序列化。
        /// <para>注意：Byte[]类型不用考虑。内部单独会做处理。</para>
        /// </summary>
        /// <param name="converter"></param>
        public void SetConverter(BytesConverter converter)
        {
            Converter = converter;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="e"></param>
        protected override void OnHandshaked(IDependencyTouchRpc client, VerifyOptionEventArgs e)
        {
            client.SetValue(RedisClientExtensions.RedisClientProperty, new InternalRedisClient(client.RpcActor, Converter));
            base.OnHandshaked(client, e);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="e"></param>
        protected override void OnReceivedProtocolData(IDependencyTouchRpc client, ProtocolDataEventArgs e)
        {
            switch (e.Protocol)
            {
                case TouchRpcUtility.P_600_Redis_Request:
                    {
                        var waitResult = new RedisResponseWaitPackage();
                        try
                        {
                            e.Handled = true;
                            RedisRequestWaitPackage package = new RedisRequestWaitPackage();
                            package.Unpackage(e.ByteBlock.Seek(2));
                            waitResult.Sign = package.Sign;

                            switch (package.packageType)
                            {
                                case RedisPackageType.Set:
                                    {
                                        bool success = ICache.SetCache(new CacheEntry<string, byte[]>(package.key)
                                        {
                                            Duration = package.timeSpan.Value,
                                            Value = package.value
                                        });
                                        if (success)
                                        {
                                            waitResult.Status = 1;
                                        }
                                        else
                                        {
                                            waitResult.Status = byte.MaxValue;
                                        }
                                        break;
                                    }
                                case RedisPackageType.Get:
                                    {
                                        var cache = ICache.GetCache(package.key);
                                        if (cache != null)
                                        {
                                            waitResult.Status = 1;
                                            waitResult.value = cache.Value;
                                        }
                                        else
                                        {
                                            waitResult.Status = byte.MaxValue;
                                        }
                                    }
                                    break;

                                case RedisPackageType.Contains:
                                    {
                                        if (ICache.ContainsCache(package.key))
                                        {
                                            waitResult.Status = 1;
                                        }
                                        else
                                        {
                                            waitResult.Status = byte.MaxValue;
                                        }
                                    }
                                    break;

                                case RedisPackageType.Remove:
                                    {
                                        if (ICache.RemoveCache(package.key))
                                        {
                                            waitResult.Status = 1;
                                        }
                                        else
                                        {
                                            waitResult.Status = byte.MaxValue;
                                        }
                                    }
                                    break;

                                case RedisPackageType.Clear:
                                    {
                                        ICache.ClearCache();
                                        waitResult.Status = 1;
                                    }
                                    break;

                                default:
                                    return;
                            }
                        }
                        catch (Exception ex)
                        {
                            waitResult.Status = 2;
                            waitResult.Message = ex.Message;
                        }

                        using (ByteBlock byteBlock = new ByteBlock())
                        {
                            waitResult.Package(byteBlock);
                            client.Send(TouchRpcUtility.P_1600_Redis_Response, byteBlock.Buffer, 0, byteBlock.Len);
                        }
                        break;
                    }

                case TouchRpcUtility.P_1600_Redis_Response:
                    {
                        e.Handled = true;
                        var waitResult = new RedisResponseWaitPackage();
                        waitResult.Unpackage(e.ByteBlock.Seek(2));
                        client.RpcActor.WaitHandlePool.SetRun(waitResult);
                        break;
                    }
                default:
                    break;
            }
        }
    }
}