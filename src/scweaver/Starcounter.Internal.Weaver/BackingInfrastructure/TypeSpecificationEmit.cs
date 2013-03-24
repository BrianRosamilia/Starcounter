﻿
using PostSharp.Sdk.CodeModel;
using Starcounter.Binding;
using Starcounter.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Internal.Weaver.BackingInfrastructure {
    /// <summary>
    /// Encapsulates the emission of type specifications according
    /// to <see cref="http://www.starcounter.com/internal/wiki/W3#Type_specification"/>.
    /// </summary>
    /// <remarks>
    /// Code hosts that want to safely consume the emitted infrastructure should
    /// use the corresponding <see cref="TypeSpecification"/> type.
    /// </remarks>
    internal sealed class TypeSpecificationEmit {
        AssemblySpecificationEmit assemblySpec;
        TypeDefDeclaration typeDef;
        TypeDefDeclaration emittedSpec;

        public FieldDefDeclaration TableHandle {
            get;
            private set;
        }

        public FieldDefDeclaration TypeBindingReference {
            get;
            private set;
        }

        public FieldDefDeclaration ThisHandle {
            get;
            private set;
        }

        public FieldDefDeclaration ThisIdentity {
            get;
            private set;
        }

        public FieldDefDeclaration ThisBinding {
            get;
            private set;
        }

        public TypeSpecificationEmit BaseSpecification {
            get {
                var parentType = typeDef.BaseType;
                var parentTypeDef = parentType as TypeDefDeclaration;
                return assemblySpec.GetSpecification(parentTypeDef);
            }
        }

        public TypeSpecificationEmit(AssemblySpecificationEmit assemblySpecEmit, TypeDefDeclaration typeDef) {
            this.assemblySpec = assemblySpecEmit;
            this.typeDef = typeDef;
            EmitSpecification();
        }

        void EmitSpecification() {
            emittedSpec = new TypeDefDeclaration {
                Name = TypeSpecification.Name,
                Attributes = TypeAttributes.Class | TypeAttributes.NestedAssembly | TypeAttributes.Sealed
            };
            typeDef.Types.Add(emittedSpec);

            var tableHandle = new FieldDefDeclaration {
                Name = TypeSpecification.TableHandleName,
                Attributes = (FieldAttributes.FamORAssem | FieldAttributes.Static),
                FieldType = assemblySpec.UInt16Type
            };
            emittedSpec.Fields.Add(tableHandle);
            this.TableHandle = tableHandle;

            var typeBindingReference = new FieldDefDeclaration {
                Name = TypeSpecification.TypeBindingName,
                Attributes = (FieldAttributes.FamORAssem | FieldAttributes.Static),
                FieldType = assemblySpec.TypeBindingType
            };
            emittedSpec.Fields.Add(typeBindingReference);
            this.TypeBindingReference = typeBindingReference;

            if (ScTransformTask.InheritsObject(typeDef)) {
                var thisHandle = new FieldDefDeclaration {
                    Name = TypeSpecification.ThisHandleName,
                    Attributes = FieldAttributes.Family,
                    FieldType = assemblySpec.UInt64Type
                };
                typeDef.Fields.Add(thisHandle);
                this.ThisHandle = thisHandle;

                var thisId = new FieldDefDeclaration {
                    Name = TypeSpecification.ThisIdName,
                    Attributes = FieldAttributes.Family,
                    FieldType = assemblySpec.UInt64Type
                };
                typeDef.Fields.Add(thisId);
                this.ThisIdentity = thisId;

                var thisBinding = new FieldDefDeclaration {
                    Name = TypeSpecification.ThisBindingName,
                    Attributes = FieldAttributes.Family,
                    FieldType = assemblySpec.TypeBindingType
                };
                typeDef.Fields.Add(thisBinding);
                this.ThisBinding = thisBinding;

            } else {
                this.ThisHandle = typeDef.FindField(TypeSpecification.ThisHandleName).Field;
                this.ThisBinding = typeDef.FindField(TypeSpecification.ThisBindingName).Field;
                this.ThisIdentity = typeDef.FindField(TypeSpecification.ThisIdName).Field;
            }
        }

        public FieldDefDeclaration IncludeField(FieldDefDeclaration field) {
            var specType = this.emittedSpec;
            var columnHandle = new FieldDefDeclaration {
                Name = TypeSpecification.FieldNameToColumnHandleName(field.Name),
                Attributes = FieldAttributes.Public | FieldAttributes.Static,
                FieldType = assemblySpec.Int32Type
            };
            specType.Fields.Add(columnHandle);
            return columnHandle;
        }
    }
}
