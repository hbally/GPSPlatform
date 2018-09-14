﻿using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JT808.Protocol;
using JT808.Protocol.Extensions;
using DotNetty.Common.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JT808.Protocol.Exceptions;

namespace GPS.JT808NettyServer.Handlers
{
    public class JT808ServiceHandler : ChannelHandlerAdapter
    {
        private readonly ILogger<JT808ServiceHandler> logger;

        private readonly SessionManager sessionManager;

        private readonly JT808MsgIdHandler jT808MsgIdHandler;

        public JT808ServiceHandler(
            JT808MsgIdHandler jT808MsgIdHandler,
            SessionManager sessionManager,
            ILoggerFactory loggerFactory)
        {
            this.jT808MsgIdHandler = jT808MsgIdHandler;
            this.sessionManager = sessionManager;
            logger = loggerFactory.CreateLogger<JT808ServiceHandler>();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var buffer = (JT808RequestInfo)message;
            string receive = buffer.OriginalBuffer.ToHexString();
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("receive<<<"+receive);
            }
            try
            {
                Func<JT808RequestInfo,IJT808Package> handlerFunc;
                if (jT808MsgIdHandler.HandlerDict.TryGetValue(buffer.JT808Package.Header.MsgId,  out handlerFunc))
                {
                    sessionManager.RegisterSession(new JT808Session(context.Channel, buffer.JT808Package.Header.TerminalPhoneNo));
                    IJT808Package jT808PackageImpl = handlerFunc(buffer);
                    if (jT808PackageImpl != null)
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("send>>>" + jT808PackageImpl.JT808Package.Header.MsgId.ToString() + "-" + JT808Serializer.Serialize(jT808PackageImpl.JT808Package).ToHexString());
                            logger.LogDebug("send>>>" + jT808PackageImpl.JT808Package.Header.MsgId.ToString() + "-" + JsonConvert.SerializeObject(jT808PackageImpl.JT808Package));
                        }
                        // 需要注意：
                        // 1.下发应答必须要在类中重写 ChannelReadComplete 不然客户端接收不到消息
                        // context.WriteAsync(Unpooled.WrappedBuffer(JT808Serializer.Serialize(jT808PackageImpl.JT808Package)));
                        // 2.直接发送
                        context.WriteAndFlushAsync(Unpooled.WrappedBuffer(JT808Serializer.Serialize(jT808PackageImpl.JT808Package)));
                    }
                }
            }
            catch (JT808Exception ex)
            {
                logger.LogError(ex, "JT808Exception receive<<<" + receive);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception receive<<<" + receive);
            }
        }

        //public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();
    }
}