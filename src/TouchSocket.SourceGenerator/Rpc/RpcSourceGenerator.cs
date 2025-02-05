﻿using Microsoft.CodeAnalysis;
using System.Linq;

namespace TouchSocket
{
    /// <summary>
    /// HttpApi代码生成器
    /// </summary>
    [Generator]
    public class RpcSourceGenerator : ISourceGenerator
    {

        readonly string m_generatorRpcMethodAttribute = @"
using System;

namespace TouchSocket.Rpc
{
    /// <summary>
    /// 标识该接口方法将自动生成调用的扩展方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class GeneratorRpcMethodAttribute : RpcAttribute
    {

    }
}
";
        readonly string m_generatorRpcProxyAttribute = @"
using System;

namespace TouchSocket.Rpc
{
    /// <summary>
    /// 标识该接口将自动生成调用的扩展方法静态类
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    internal sealed class GeneratorRpcProxyAttribute : Attribute
    {
        /// <summary>
        /// 调用键的前缀，包括服务的命名空间，类名，不区分大小写。格式：命名空间.类名
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// 生成泛型方法的约束
        /// </summary>
        public Type[] GenericConstraintTypes { get; set; }

        /// <summary>
        /// 是否仅以函数名调用，当为True是，调用时仅需要传入方法名即可。
        /// </summary>
        public bool MethodInvoke { get; set; }

        /// <summary>
        /// 生成代码的命名空间
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// 生成的类名，不要包含“I”，生成接口时会自动家。
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// 生成代码
        /// </summary>
        public CodeGeneratorFlag GeneratorFlag { get; set; }

        /// <summary>
        /// 函数标识
        /// </summary>
        public MethodFlags MethodFlags { get; set; }

        /// <summary>
        /// 继承接口
        /// </summary>
        public bool InheritedInterface { get; set; }
    }
}

";
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(a=>
            {
                a.AddSource(nameof(this.m_generatorRpcMethodAttribute), m_generatorRpcMethodAttribute);
                a.AddSource(nameof(this.m_generatorRpcProxyAttribute), m_generatorRpcProxyAttribute);
            });
            context.RegisterForSyntaxNotifications(() => new RpcSyntaxReceiver());
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context"></param>
        public void Execute(GeneratorExecutionContext context)
        {
            var s = context.Compilation.GetMetadataReference(context.Compilation.Assembly);

            if (context.SyntaxReceiver is RpcSyntaxReceiver receiver)
            {
                var builders = receiver
                    .GetRpcApiTypes(context.Compilation)
                    .Select(i => new RpcCodeBuilder(i))
                    .Distinct();
                //Debugger.Launch();
                foreach (var builder in builders)
                {
                    context.AddSource($"{builder.GetFileName()}.g.cs", builder.ToSourceText());
                }
            }
        }
    }
}
