using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.JsonRpc.Generators.Contexts;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static OmniSharp.Extensions.JsonRpc.Generators.Helpers;

namespace OmniSharp.Extensions.JsonRpc.Generators.Strategies
{
    internal class HandlerGeneratorStrategy : ICompilationUnitGeneratorStrategy
    {
        public IEnumerable<MemberDeclarationSyntax> Apply(GeneratorData item)
        {
            if (item.JsonRpcAttributes.GenerateHandler is not { }) yield break;
            var members = new List<MemberDeclarationSyntax>();

            var resolver = GeneratorData.CreateForResolver(item);

            var attributesToCopy = item.TypeDeclaration.AttributeLists
                                       .Select(z => z.Attributes.Where(AttributeFilter))
                                       .Where(z => z.Any())
                                       .Select(z => AttributeList(SeparatedList(z)))
                                       .Concat(
                                            new[] {
                                                AttributeList(
                                                    SeparatedList(
                                                        new[] {
                                                            Attribute(ParseName("System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                                                        }
                                                    )
                                                )
                                            }
                                        )
                                       .ToArray();

            var handlerInterface = InterfaceDeclaration(Identifier($"I{item.JsonRpcAttributes.HandlerName}Handler"))
                                  .WithAttributeLists(List(attributesToCopy))
                                  .WithModifiers(item.TypeDeclaration.Modifiers)
                                  .AddBaseListTypes(
                                       SimpleBaseType(GetBaseHandlerInterface(item))
                                   );

            if (GetRegistrationAndOrCapability(item) is { } registrationAndOrCapability)
            {
                handlerInterface = handlerInterface.AddBaseListTypes(SimpleBaseType(registrationAndOrCapability));
            }

            var classAttributes =
                SingletonList(
                    AttributeList(
                        SeparatedList(
                            new[] {
                                Attribute(ParseName("System.Runtime.CompilerServices.CompilerGeneratedAttribute")),
                                Attribute(ParseName("System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute")),
                            }.Concat(
                                attributesToCopy
                                   .SelectMany(z => z.Attributes)
                                   .Where(z => z.Name.ToFullString().Contains("Obsolete"))
                            ).ToArray()
                        )
                    )
                );

            {
                var baseClass = GetBaseHandlerClass(item);
                var handlerClass = ClassDeclaration(Identifier($"{item.JsonRpcAttributes.HandlerName}HandlerBase"))
                                  .WithAttributeLists(classAttributes)
                                  .AddModifiers(Token(SyntaxKind.AbstractKeyword))
                                  .AddModifiers(item.TypeDeclaration.Modifiers.ToArray());

                if (baseClass is { })
                {
                    handlerClass = handlerClass.AddBaseListTypes(SimpleBaseType(baseClass));
                }

                handlerClass = handlerClass.AddBaseListTypes(SimpleBaseType(IdentifierName($"I{item.JsonRpcAttributes.HandlerName}Handler")));

                if (resolver is { })
                {
                    handlerClass = handlerClass
                       .AddBaseListTypes(SimpleBaseType(IdentifierName($"I{resolver.JsonRpcAttributes.HandlerName}Handler")));
                }

                if (item.LspAttributes?.CanBeResolved == true && item is RequestItem r)
                {
                    handlerInterface = handlerInterface.AddBaseListTypes(SimpleBaseType(IdentifierName("ICanBeIdentifiedHandler")));
                    handlerClass = handlerClass
                                  .AddMembers(AddConstructors(r, r, false).ToArray())
                                  .AddMembers(AddCanBeIdentifiedMembers().ToArray());
                }
                else if (resolver is { } && item is RequestItem r2)
                {
                    handlerClass = handlerClass
                                  .AddMembers(AddConstructors(r2, resolver, false).ToArray())
                                  .AddMembers(AddCanBeIdentifiedMembers().ToArray())
                                  .AddMembers(AddResolveMethod(resolver.Request.Syntax));
                }

                members.Add(handlerInterface);
                members.Add(handlerClass);

                if (item is RequestItem r3)
                {
                    if (resolver is { LspAttributes: { GenerateTypedData: true } })
                    {
                        members.Add(
                            GenerateTypedHandler(
                                handlerClass,
                                item.Request.Syntax,
                                r3.Response.Syntax,
                                resolver.Request.Symbol.Name
                            )
                        );
                    }
                }
            }

            if (item is RequestItem request)
            {
                ClassDeclarationSyntax? handlerClass = null;
                if (request is { PartialItem: { } })
                {
                    handlerClass = ClassDeclaration(Identifier($"{item.JsonRpcAttributes.HandlerName}PartialHandlerBase"))
                                  .WithAttributeLists(classAttributes)
                                  .AddModifiers(Token(SyntaxKind.AbstractKeyword))
                                  .AddModifiers(item.TypeDeclaration.Modifiers.ToArray())
                                  .AddBaseListTypes(
                                       SimpleBaseType(GetPartialResultBaseClass(request, request.PartialItem)),
                                       SimpleBaseType(IdentifierName($"I{item.JsonRpcAttributes.HandlerName}Handler"))
                                   );
                    if (item.LspAttributes?.CanBeResolved == true && item is RequestItem r)
                    {
                        handlerClass = handlerClass
                                      .AddMembers(AddConstructors(r, r, true).ToArray())
                                      .AddMembers(AddCanBeIdentifiedMembers().ToArray());
                    }
                    else if (resolver is { })
                    {
                        handlerClass = handlerClass
                                      .AddMembers(AddConstructors(request, resolver, true).ToArray())
                                      .AddBaseListTypes(SimpleBaseType(IdentifierName($"I{resolver.JsonRpcAttributes.HandlerName}Handler")))
                                      .AddMembers(AddCanBeIdentifiedMembers().ToArray())
                                      .AddMembers(AddResolveMethod(resolver.Request.Syntax));
                    }
                    else
                    {
                        handlerClass = handlerClass
                           .AddMembers(AddConstructors(request, resolver, true).ToArray());
                    }

                    members.Add(handlerClass);
                }
                else if (request is { PartialItems: { } })
                {
                    handlerClass = ClassDeclaration(Identifier($"{item.JsonRpcAttributes.HandlerName}PartialHandlerBase"))
                                  .WithAttributeLists(classAttributes)
                                  .AddModifiers(Token(SyntaxKind.AbstractKeyword))
                                  .AddModifiers(item.TypeDeclaration.Modifiers.ToArray())
                                  .AddBaseListTypes(
                                       SimpleBaseType(GetPartialResultsBaseClass(request, request.PartialItems)),
                                       SimpleBaseType(IdentifierName($"I{item.JsonRpcAttributes.HandlerName}Handler"))
                                   );

                    if (item.LspAttributes?.CanBeResolved == true && item is RequestItem r)
                    {
                        handlerClass = handlerClass
                                      .AddMembers(AddConstructors(r, r, true).ToArray())
                                      .AddMembers(AddCanBeIdentifiedMembers().ToArray());
                    }
                    else if (resolver is { })
                    {
                        handlerClass = handlerClass
                                      .AddMembers(AddConstructors(request, resolver, true).ToArray())
                                      .AddBaseListTypes(SimpleBaseType(IdentifierName($"I{resolver.JsonRpcAttributes.HandlerName}Handler")))
                                      .AddMembers(AddCanBeIdentifiedMembers().ToArray())
                                      .AddMembers(AddResolveMethod(resolver.Request.Syntax));
                    }
                    else
                    {
                        handlerClass = handlerClass
                           .AddMembers(AddConstructors(request, resolver, true).ToArray());
                    }

                    members.Add(handlerClass);
                }

                if (resolver is { LspAttributes: { GenerateTypedData: true } } && handlerClass is { })
                {
                    members.Add(
                        GenerateTypedPartialHandler(
                            handlerClass,
                            item.Request.Syntax,
                            request.Response.Syntax,
                            resolver.Request.Symbol.Name
                        )
                    );
                }

                if (item is { LspAttributes: { GenerateTypedData: true } } && handlerClass is { })
                {
                    members.Add(
                        GenerateTypedPartialHandler(
                            handlerClass,
                            item.Request.Syntax,
                            request.Response.Syntax,
                            item.Request.Symbol.Name
                        )
                    );
                }
            }

            if (members.Count == 0) yield break;

            yield return NamespaceDeclaration(ParseName(item.JsonRpcAttributes.HandlerNamespace))
                        .WithMembers(List(members))
                        .WithLeadingTrivia(TriviaList(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true))))
                        .WithTrailingTrivia(TriviaList(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.RestoreKeyword), true))))
                        .NormalizeWhitespace();
        }

        private static GenericNameSyntax GetBaseHandlerInterface(GeneratorData item)
        {
            if (item is NotificationItem notification)
            {
                return GenericName("IJsonRpcNotificationHandler")
                   .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(notification.Request.Syntax)));
            }

            if (item is RequestItem request)
            {
                if (request.IsUnit)
                {
                    return GenericName("IJsonRpcRequestHandler")
                       .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(request.Request.Syntax)));
                }

                return GenericName("IJsonRpcRequestHandler")
                   .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { request.Request.Syntax, request.Response.Syntax })));
            }

            throw new NotSupportedException();
        }

        private static TypeSyntax? GetBaseHandlerClass(GeneratorData item)
        {
            var onlyCapability = item is { Capability: { }, RegistrationOptions: null };
            var types = new List<TypeSyntax>() { item.Request.Syntax };
            if (item.RegistrationOptions is { })
            {
                types.Add(item.RegistrationOptions.Syntax);
            }

            if (item.Capability is { })
            {
                types.Add(item.Capability.Syntax);
            }

            if (item is NotificationItem notification)
            {
                return QualifiedName(
                    IdentifierName("AbstractHandlers"),
                    GenericName($"Notification{( onlyCapability ? "Capability" : "" )}")
                       .WithTypeArgumentList(TypeArgumentList(SeparatedList(types)))
                );
            }

            if (item is RequestItem request)
            {
                types.Insert(1, request.Response.Syntax);
                return QualifiedName(
                    IdentifierName("AbstractHandlers"),
                    GenericName($"Request{( onlyCapability ? "Capability" : "" )}")
                       .WithTypeArgumentList(TypeArgumentList(SeparatedList(types)))
                );
            }

            return null;
        }

        private static TypeSyntax GetPartialResultBaseClass(RequestItem request, SyntaxSymbol item)
        {
            var onlyCapability = request is { Capability: { }, RegistrationOptions: null };
            var types = new List<TypeSyntax> { request.Request.Syntax, request.Response.Syntax, item.Syntax };

            if (request.RegistrationOptions is { })
            {
                types.Add(request.RegistrationOptions.Syntax);
            }

            if (request.Capability is { })
            {
                types.Add(request.Capability.Syntax);
            }

            return QualifiedName(
                IdentifierName("AbstractHandlers"),
                GenericName($"PartialResult{( onlyCapability ? "Capability" : "" )}")
                   .WithTypeArgumentList(TypeArgumentList(SeparatedList(types)))
            );
        }

        private static TypeSyntax GetPartialResultsBaseClass(RequestItem request, SyntaxSymbol item)
        {
            var onlyCapability = request is { Capability: { }, RegistrationOptions: null };
            var types = new List<TypeSyntax> { request.Request.Syntax, request.Response.Syntax, item.Syntax };

            if (request.RegistrationOptions is { })
            {
                types.Add(request.RegistrationOptions.Syntax);
            }

            if (request.Capability is { })
            {
                types.Add(request.Capability.Syntax);
            }

            return QualifiedName(
                IdentifierName("AbstractHandlers"),
                GenericName($"PartialResults{( onlyCapability ? "Capability" : "" )}")
                   .WithTypeArgumentList(TypeArgumentList(SeparatedList(types)))
            );
        }

        private static GenericNameSyntax? GetRegistrationAndOrCapability(GeneratorData item)
        {
            if (item.Capability is { } && item.RegistrationOptions is { })
            {
                return GenericName("IRegistration")
                   .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { item.RegistrationOptions.Syntax, item.Capability.Syntax })));
            }

            if (item.RegistrationOptions is { })
            {
                return GenericName("IRegistration")
                   .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(item.RegistrationOptions.Syntax)));
            }

            if (item.Capability is { })
            {
                return GenericName("ICapability")
                   .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(item.Capability.Syntax)));
            }

            return null;
        }

        private static bool AttributeFilter(AttributeSyntax syntax)
        {
            var fullString = syntax.ToFullString();
            return !fullString.Contains("Generate") && !fullString.Contains("DebuggerDisplay") && !fullString.Contains("RegistrationOptions") && !fullString.Contains("Capability")
                && !fullString.Contains("Resolver");
        }

        private static ConstructorDeclarationSyntax GetConstructor(
            RequestItem request,
            ParameterListSyntax parameters,
            ConstructorInitializerSyntax constructorInitializerSyntax,
            bool isPartial
        )
        {
            return ConstructorDeclaration(
                       Identifier(
                           isPartial
                               ? $"{request.JsonRpcAttributes.HandlerName}PartialHandlerBase"
                               : $"{request.JsonRpcAttributes.HandlerName}HandlerBase"
                       )
                   )
                  .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)))
                  .WithParameterList(parameters)
                  .WithInitializer(constructorInitializerSyntax)
                  .WithBody(Block());
        }

        private static MemberDeclarationSyntax AddResolveMethod(TypeSyntax syntax)
        {
            return MethodDeclaration(
                       GenericName(Identifier("Task")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(syntax))),
                       Identifier("Handle")
                   )
                  .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword)))
                  .WithParameterList(
                       ParameterList(
                           SeparatedList(
                               new[] {
                                   Parameter(Identifier("request")).WithType(syntax),
                                   Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                               }
                           )
                       )
                   )
                  .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static IEnumerable<MemberDeclarationSyntax> AddCanBeIdentifiedMembers()
        {
            yield return FieldDeclaration(VariableDeclaration(IdentifierName("Guid")).WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("_id")))))
               .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword)));
            yield return PropertyDeclaration(IdentifierName("Guid"), Identifier("Id"))
                        .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(IdentifierName("ICanBeIdentifiedHandler")))
                        .WithExpressionBody(ArrowExpressionClause(IdentifierName("_id")))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static IEnumerable<MemberDeclarationSyntax> AddConstructors(RequestItem request, RequestItem? resolver, bool isPartial)
        {
            var parameters = ParameterList();
            var arguments = ArgumentList();
            if (isPartial)
            {
                parameters = parameters.AddParameters(Parameter(Identifier("progressManager")).WithType(IdentifierName("IProgressManager")));
                arguments = arguments.AddArguments(Argument(IdentifierName("progressManager")));

                if (request.PartialItem is { })
                {
                    arguments = arguments.AddArguments(
                        Argument(
                            SimpleLambdaExpression(Parameter(Identifier("item")))
                               .WithExpressionBody(IdentifierName("item"))
                        )
                    );
                }
                else if (request.PartialItems is { })
                {
                    arguments = arguments.AddArguments(
                        Argument(
                            SimpleLambdaExpression(Parameter(Identifier("enumerable")))
                               .WithExpressionBody(
                                    ObjectCreationExpression(
                                            request.Response.Syntax is NullableTypeSyntax nts
                                                ? nts.ElementType
                                                : request.Response.Syntax
                                        )
                                       .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("enumerable")))))
                                )
                        )
                    );
                }
            }

            if (resolver is { })
            {
                yield return GetConstructor(
                        request,
                        ParameterList(
                            SingletonSeparatedList(
                                Parameter(Identifier("id")).WithType(
                                    IdentifierName("System.Guid")
                                )
                            )
                        ).AddParameters(parameters.Parameters.ToArray()),
                        ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments),
                        isPartial
                    )
                   .WithBody(
                        Block(
                            SingletonList<StatementSyntax>(
                                ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName("_id"), IdentifierName("id"))
                                )
                            )
                        )
                    );
                yield return GetConstructor(
                    request,
                    parameters,
                    ConstructorInitializer(
                        SyntaxKind.ThisConstructorInitializer,
                        ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("Guid"),
                                                IdentifierName("NewGuid")
                                            )
                                        )
                                    )
                                )
                            )
                           .AddArguments(arguments.Arguments.Take(Math.Max(0, arguments.Arguments.Count - 1)).ToArray())
                    ),
                    isPartial
                );
            }
            else
            {
                yield return GetConstructor(
                    request,
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(Identifier("id")).WithType(
                                IdentifierName("System.Guid")
                            )
                        )
                    ).AddParameters(parameters.Parameters.ToArray()),
                    ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments),
                    isPartial
                );
            }
        }

        private static ClassDeclarationSyntax GenerateTypedPartialHandler(
            RequestItem item,
            ClassDeclarationSyntax classDeclarationSyntax,
            TypeSyntax paramsName,
            TypeSyntax containerName,
            string resolveName
        )
        {
            return ClassDeclaration(classDeclarationSyntax.Identifier)
                  .WithModifiers(classDeclarationSyntax.Modifiers)
                  .WithAttributeLists(classDeclarationSyntax.AttributeLists)
                  .WithHandlerIdentityConstraint()
                  .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(IdentifierName(classDeclarationSyntax.Identifier.Text)))))
                  .AddMembers(
                       ConstructorDeclaration(classDeclarationSyntax.Identifier)
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("id")).WithType(IdentifierName("Guid")),
                                           Parameter(Identifier("progressManager")).WithType(IdentifierName("IProgressManager"))
                                       }
                                   )
                               )
                           )
                          .WithInitializer(
                               ConstructorInitializer(
                                   SyntaxKind.BaseConstructorInitializer,
                                   ArgumentList(
                                       SeparatedList(
                                           new[] {
                                               Argument(IdentifierName("id")),
                                               Argument(
                                                   IdentifierName("progressManager")
                                               )
                                           }
                                       )
                                   )
                               )
                           )
                          .WithBody(Block()),
                       ConstructorDeclaration(classDeclarationSyntax.Identifier)
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)))
                          .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("progressManager")).WithType(IdentifierName("IProgressManager")))))
                          .WithInitializer(
                               ConstructorInitializer(
                                   SyntaxKind.ThisConstructorInitializer,
                                   ArgumentList(
                                       SeparatedList(
                                           new[] {
                                               Argument(
                                                   InvocationExpression(
                                                       MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("Guid"), IdentifierName("NewGuid"))
                                                   )
                                               ),
                                               Argument(
                                                   IdentifierName("progressManager")
                                               )
                                           }
                                       )
                                   )
                               )
                           )
                          .WithBody(Block()),
                       MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("Handle"))
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.OverrideKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(paramsName),
                                           Parameter(Identifier("results"))
                                              .WithType(
                                                   GenericName(Identifier("IObserver"))
                                                      .WithTypeArgumentList(
                                                           TypeArgumentList(
                                                               SingletonSeparatedList<TypeSyntax>(
                                                                   GenericName(Identifier("IEnumerable"))
                                                                      .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName(resolveName))))
                                                               )
                                                           )
                                                       )
                                               ),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithExpressionBody(
                               ArrowExpressionClause(
                                   InvocationExpression(IdentifierName("Handle"))
                                      .WithArgumentList(
                                           ArgumentList(
                                               SeparatedList(
                                                   new[] {
                                                       Argument(IdentifierName("request")),
                                                       Argument(
                                                           ObjectCreationExpression(
                                                                   QualifiedName(
                                                                       IdentifierName("LanguageProtocolDelegatingHandlers"),
                                                                       GenericName(
                                                                               Identifier("TypedPartialObserver")
                                                                           )
                                                                          .WithTypeArgumentList(
                                                                               TypeArgumentList(
                                                                                   SeparatedList(
                                                                                       new TypeSyntax[] {
                                                                                           GenericName(
                                                                                                   Identifier(resolveName)
                                                                                               )
                                                                                              .WithTypeArgumentList(
                                                                                                   TypeArgumentList(
                                                                                                       SingletonSeparatedList<TypeSyntax>(
                                                                                                           IdentifierName("T")
                                                                                                       )
                                                                                                   )
                                                                                               ),
                                                                                           IdentifierName(resolveName)
                                                                                       }
                                                                                   )
                                                                               )
                                                                           )
                                                                   )
                                                               )
                                                              .WithArgumentList(
                                                                   ArgumentList(
                                                                       SeparatedList(
                                                                           new[] {
                                                                               Argument(
                                                                                   IdentifierName("results")
                                                                               ),
                                                                               Argument(
                                                                                   MemberAccessExpression(
                                                                                       SyntaxKind.SimpleMemberAccessExpression,
                                                                                       IdentifierName(resolveName),
                                                                                       IdentifierName("From")
                                                                                   )
                                                                               )
                                                                           }
                                                                       )
                                                                   )
                                                               )
                                                       ),
                                                       Argument(IdentifierName("cancellationToken"))
                                                   }
                                               )
                                           )
                                       )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                       MethodDeclaration(
                               GenericName(Identifier("Task"))
                                  .WithTypeArgumentList(
                                       TypeArgumentList(
                                           SingletonSeparatedList<TypeSyntax>(
                                               IdentifierName(resolveName)
                                           )
                                       )
                                   ),
                               Identifier("Handle")
                           )
                          .WithModifiers(
                               TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.OverrideKeyword), Token(SyntaxKind.AsyncKeyword))
                           )
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(IdentifierName(resolveName)),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithBody(
                               Block(
                                   LocalDeclarationStatement(
                                       VariableDeclaration(IdentifierName("var"))
                                          .WithVariables(
                                               SingletonSeparatedList(
                                                   VariableDeclarator(Identifier("response"))
                                                      .WithInitializer(
                                                           EqualsValueClause(
                                                               AwaitExpression(
                                                                   InvocationExpression(
                                                                           MemberAccessExpression(
                                                                               SyntaxKind.SimpleMemberAccessExpression,
                                                                               InvocationExpression(
                                                                                       IdentifierName("HandleResolve")
                                                                                   )
                                                                                  .WithArgumentList(
                                                                                       ArgumentList(
                                                                                           SeparatedList(
                                                                                               new[] {
                                                                                                   Argument(IdentifierName("request")),
                                                                                                   Argument(
                                                                                                       IdentifierName("cancellationToken")
                                                                                                   )
                                                                                               }
                                                                                           )
                                                                                       )
                                                                                   ),
                                                                               IdentifierName("ConfigureAwait")
                                                                           )
                                                                       )
                                                                      .WithArgumentList(
                                                                           ArgumentList(
                                                                               SingletonSeparatedList(Argument(LiteralExpression(SyntaxKind.FalseLiteralExpression)))
                                                                           )
                                                                       )
                                                               )
                                                           )
                                                       )
                                               )
                                           )
                                   ),
                                   ReturnStatement(IdentifierName("response"))
                               )
                           ),
                       MethodDeclaration(
                               PredefinedType(Token(SyntaxKind.VoidKeyword)),
                               Identifier("Handle")
                           )
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(paramsName),
                                           Parameter(Identifier("results"))
                                              .WithType(
                                                   GenericName(Identifier("IObserver"))
                                                      .WithTypeArgumentList(
                                                           TypeArgumentList(
                                                               SingletonSeparatedList<TypeSyntax>(
                                                                   GenericName(Identifier("IEnumerable"))
                                                                      .WithTypeArgumentList(
                                                                           TypeArgumentList(
                                                                               SingletonSeparatedList<TypeSyntax>(
                                                                                   GenericName(
                                                                                           Identifier(resolveName)
                                                                                       )
                                                                                      .WithTypeArgumentList(
                                                                                           TypeArgumentList(
                                                                                               SingletonSeparatedList<TypeSyntax>(
                                                                                                   IdentifierName("T")
                                                                                               )
                                                                                           )
                                                                                       )
                                                                               )
                                                                           )
                                                                       )
                                                               )
                                                           )
                                                       )
                                               ),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                       MethodDeclaration(
                               GenericName(Identifier("Task"))
                                  .WithTypeArgumentList(
                                       TypeArgumentList(
                                           SingletonSeparatedList<TypeSyntax>(
                                               GenericName(Identifier(resolveName))
                                                  .WithTypeArgumentList(
                                                       TypeArgumentList(
                                                           SingletonSeparatedList<TypeSyntax>(
                                                               IdentifierName("T")
                                                           )
                                                       )
                                                   )
                                           )
                                       )
                                   ),
                               Identifier("HandleResolve")
                           )
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request"))
                                              .WithType(
                                                   GenericName(Identifier(resolveName))
                                                      .WithTypeArgumentList(
                                                           TypeArgumentList(
                                                               SingletonSeparatedList<TypeSyntax>(
                                                                   IdentifierName("T")
                                                               )
                                                           )
                                                       )
                                               ),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                   );
        }

        private static ClassDeclarationSyntax GenerateTypedHandler(
            RequestItem item,
            ClassDeclarationSyntax classDeclarationSyntax,
            TypeSyntax paramsName,
            TypeSyntax containerName,
            string resolveName
        )
        {
            // determine the resolve item based on the partial items of the request
            var responseName = containerName is SimpleNameSyntax sns ? sns.Identifier.Text : containerName.ToFullString();
            TypeSyntax responseType = containerName;
            // Special case... because the spec is awesome
            if (responseName == "CommandOrCodeActionContainer")
            {
                responseType = GenericName(Identifier("CodeActionContainer"))
                   .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T"))));
            }

            return ClassDeclaration(classDeclarationSyntax.Identifier)
                  .WithModifiers(classDeclarationSyntax.Modifiers)
                  .WithAttributeLists(classDeclarationSyntax.AttributeLists)
                  .WithHandlerIdentityConstraint()
                  .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(IdentifierName(classDeclarationSyntax.Identifier.Text)))))
                  .AddMembers(
                       ConstructorDeclaration(classDeclarationSyntax.Identifier)
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)))
                          .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("id")).WithType(IdentifierName("Guid")))))
                          .WithInitializer(
                               ConstructorInitializer(
                                   SyntaxKind.BaseConstructorInitializer,
                                   ArgumentList(SingletonSeparatedList(Argument(IdentifierName("id"))))
                               )
                           )
                          .WithBody(Block()),
                       ConstructorDeclaration(classDeclarationSyntax.Identifier)
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)))
                          .WithInitializer(
                               ConstructorInitializer(
                                   SyntaxKind.ThisConstructorInitializer,
                                   ArgumentList(
                                       SingletonSeparatedList(
                                           Argument(
                                               InvocationExpression(
                                                   MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("Guid"), IdentifierName("NewGuid"))
                                               )
                                           )
                                       )
                                   )
                               )
                           )
                          .WithBody(Block()),
                       MethodDeclaration(
                               GenericName(Identifier("Task")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(containerName))),
                               Identifier("Handle")
                           )
                          .WithModifiers(
                               TokenList(
                                   Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.OverrideKeyword), Token(SyntaxKind.AsyncKeyword)
                               )
                           )
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(paramsName),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithExpressionBody(
                               ArrowExpressionClause(
                                   AwaitExpression(
                                       InvocationExpression(
                                               MemberAccessExpression(
                                                   SyntaxKind.SimpleMemberAccessExpression,
                                                   InvocationExpression(IdentifierName("HandleParams"))
                                                      .WithArgumentList(
                                                           ArgumentList(
                                                               SeparatedList(
                                                                   new[] {
                                                                       Argument(IdentifierName("request")),
                                                                       Argument(IdentifierName("cancellationToken"))
                                                                   }
                                                               )
                                                           )
                                                       ),
                                                   IdentifierName("ConfigureAwait")
                                               )
                                           )
                                          .WithArgumentList(
                                               ArgumentList(
                                                   SingletonSeparatedList(
                                                       Argument(
                                                           LiteralExpression(
                                                               SyntaxKind.FalseLiteralExpression
                                                           )
                                                       )
                                                   )
                                               )
                                           )
                                   )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                       MethodDeclaration(
                               GenericName(Identifier("Task")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName(resolveName)))),
                               Identifier("Handle")
                           )
                          .WithModifiers(
                               TokenList(
                                   Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword), Token(SyntaxKind.OverrideKeyword), Token(SyntaxKind.AsyncKeyword)
                               )
                           )
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(IdentifierName(resolveName)),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithExpressionBody(
                               ArrowExpressionClause(
                                   AwaitExpression(
                                       InvocationExpression(
                                               MemberAccessExpression(
                                                   SyntaxKind.SimpleMemberAccessExpression,
                                                   InvocationExpression(IdentifierName("HandleResolve"))
                                                      .WithArgumentList(
                                                           ArgumentList(
                                                               SeparatedList(
                                                                   new[] {
                                                                       Argument(
                                                                           IdentifierName("request")
                                                                       ),
                                                                       Argument(
                                                                           IdentifierName("cancellationToken")
                                                                       )
                                                                   }
                                                               )
                                                           )
                                                       ),
                                                   IdentifierName("ConfigureAwait")
                                               )
                                           )
                                          .WithArgumentList(
                                               ArgumentList(
                                                   SingletonSeparatedList(
                                                       Argument(
                                                           LiteralExpression(
                                                               SyntaxKind.FalseLiteralExpression
                                                           )
                                                       )
                                                   )
                                               )
                                           )
                                   )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                       MethodDeclaration(
                               GenericName(Identifier("Task"))
                                  .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(responseType))),
                               Identifier("HandleParams")
                           )
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request")).WithType(paramsName),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithSemicolonToken(
                               Token(SyntaxKind.SemicolonToken)
                           ),
                       MethodDeclaration(
                               GenericName(Identifier("Task"))
                                  .WithTypeArgumentList(
                                       TypeArgumentList(
                                           SingletonSeparatedList<TypeSyntax>(
                                               GenericName(Identifier(resolveName))
                                                  .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T"))))
                                           )
                                       )
                                   ),
                               Identifier("HandleResolve")
                           )
                          .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.AbstractKeyword)))
                          .WithParameterList(
                               ParameterList(
                                   SeparatedList(
                                       new[] {
                                           Parameter(Identifier("request"))
                                              .WithType(
                                                   GenericName(Identifier(resolveName)).WithTypeArgumentList(
                                                       TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T")))
                                                   )
                                               ),
                                           Parameter(Identifier("cancellationToken")).WithType(IdentifierName("CancellationToken"))
                                       }
                                   )
                               )
                           )
                          .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                   );
        }
    }
}
