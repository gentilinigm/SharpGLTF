﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using DATATYPE = SharpGLTF.Schema2.Tiles3D.DataType;
using ELEMENTTYPE = SharpGLTF.Schema2.Tiles3D.ElementType;

namespace SharpGLTF.Schema2
{
    using Collections;
    using Memory;
    using Validation;
    using Tiles3D;
    using System.Numerics;
    using System.Text.Json.Nodes;

    partial class Tiles3DExtensions
    {
        public static EXTStructuralMetadataRoot UseStructuralMetadata(this ModelRoot modelRoot)
        {
            return modelRoot.UseExtension<EXTStructuralMetadataRoot>();
        }
    }

    namespace Tiles3D
    {
        /// <remarks>
        /// Use <see cref="Tiles3DExtensions.UseStructuralMetadata(ModelRoot)"/> to create an instance of this class.
        /// </remarks>        
        public partial class EXTStructuralMetadataRoot
        {
            #region lifecycle

            internal EXTStructuralMetadataRoot(ModelRoot modelRoot)
            {
                this.LogicalParent = modelRoot;
                _propertyTables = new ChildrenList<PropertyTable, EXTStructuralMetadataRoot>(this);
                _propertyAttributes = new ChildrenList<PropertyAttribute, EXTStructuralMetadataRoot>(this);
                _propertyTextures = new ChildrenList<PropertyTexture, EXTStructuralMetadataRoot>(this);
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                var items = base.GetLogicalChildren()
                    .Concat(_propertyTables)
                    .Concat(_propertyAttributes)
                    .Concat(_propertyTextures);

                if (Schema != null) items = items.Append(Schema);

                return items;
            }

            #endregion

            #region data

            public ModelRoot LogicalParent { get; }

            #endregion

            #region properties

            /**
            internal string SchemaUri
            {
                get => _schemaUri;
                set { _schemaUri = value; }
            }
            */

            internal StructuralMetadataSchema Schema
            {
                get => _schema;
                set { GetChildSetter(this).SetProperty(ref _schema, value); }
            }

            internal IReadOnlyList<PropertyTable> PropertyTables => _propertyTables;
            internal IReadOnlyList<PropertyAttribute> PropertyAttributes => _propertyAttributes;
            internal IReadOnlyList<PropertyTexture> PropertyTextures => _propertyTextures;

            #endregion

            #region API

            public bool TryGetEmbeddedSchema(out StructuralMetadataSchema schema)
            {
                if (_schema != null) { schema = _schema; return true; }

                schema = null;
                return false;
            }

            public StructuralMetadataSchema UseEmbeddedSchema(string id)
            {
                var schema = UseEmbeddedSchema();
                schema.Id = id;
                return schema;
            }

            /**
            // Sets the schema to use an external schema, returns an empty schema to used for adding schema properties
            public StructuralMetadataSchema UseExternalSchema(Uri uri)
            {
                SchemaUri = uri.ToString();
                return new StructuralMetadataSchema();
            }
            */

            public StructuralMetadataSchema UseEmbeddedSchema()
            {
                // SchemaUri = null;

                if (_schema == null) GetChildSetter(this).SetProperty(ref _schema, new StructuralMetadataSchema());

                return _schema;
            }

            public PropertyAttribute AddPropertyAttribute(StructuralMetadataClass schemaClass)
            {
                var prop = AddPropertyAttribute();
                prop.ClassInstance = schemaClass;
                return prop;
            }

            public PropertyAttribute AddPropertyAttribute()
            {
                var prop = new PropertyAttribute();
                _propertyAttributes.Add(prop);
                return prop;
            }

            public PropertyTable AddPropertyTable(StructuralMetadataClass schemaClass, int featureCount, string name = null)
            {
                var table = AddPropertyTable();
                table.ClassInstance = schemaClass;
                table.Count = featureCount;
                table.Name = name;
                return table;
            }

            private PropertyTable AddPropertyTable()
            {
                var prop = new PropertyTable();
                _propertyTables.Add(prop);
                return prop;
            }

            public PropertyTexture AddPropertyTexture(StructuralMetadataClass schemaClass)
            {
                var prop = AddPropertyTexture();
                prop.ClassInstance = schemaClass;
                return prop;
            }

            public PropertyTexture AddPropertyTexture()
            {
                var prop = new PropertyTexture();
                _propertyTextures.Add(prop);
                return prop;
            }

            #endregion

            #region validation

            protected override void OnValidateReferences(ValidationContext validate)
            {
                var root = LogicalParent.GetExtension<EXTStructuralMetadataRoot>();
                Guard.MustBeNull(root._schemaUri, nameof(root._schemaUri),  
                    "SchemaUri must be null, use embedded achema to set the schema");

                // Guard schema is null
                Guard.NotNull(Schema, nameof(Schema), "Schema must be defined");

                foreach (var propertyTexture in PropertyTextures)
                {
                    foreach (var propertyTextureProperty in propertyTexture.Properties)
                    {
                        var textureId = propertyTextureProperty.Value.LogicalTextureIndex;
                        validate.IsNullOrIndex(nameof(propertyTexture), textureId, LogicalParent.LogicalTextures);
                    }
                }

                foreach (var propertyTable in PropertyTables)
                {
                    Guard.NotNull(Schema.Classes[propertyTable.ClassName], nameof(propertyTable.ClassName), $"Schema must have class {propertyTable.ClassName}");

                    foreach (var property in propertyTable.Properties)
                    {
                        Guard.NotNull(Schema.Classes[propertyTable.ClassName].Properties[property.Key], nameof(property.Key), $"Schema must have property {property.Key}");

                        var values = property.Value.Values;
                        validate.IsNullOrIndex(nameof(propertyTable), values, LogicalParent.LogicalBufferViews);

                        if (property.Value.ArrayOffsets.HasValue)
                        {
                            var arrayOffsets = property.Value.ArrayOffsets.Value;
                            validate.IsNullOrIndex(nameof(propertyTable), arrayOffsets, LogicalParent.LogicalBufferViews);
                        }

                        if (property.Value.StringOffsets.HasValue)
                        {
                            var stringOffsets = property.Value.StringOffsets.Value;
                            validate.IsNullOrIndex(nameof(propertyTable), stringOffsets, LogicalParent.LogicalBufferViews);
                        }
                    }
                }

                if (Schema != null)
                {
                    foreach (var @class in Schema.Classes)
                    {
                        foreach (var property in @class.Value.Properties)
                        {
                            if (property.Value.Type == ELEMENTTYPE.ENUM)
                            {
                                Guard.IsTrue(Schema.Enums.ContainsKey(property.Value.EnumType), nameof(property.Value.EnumType), $"Enum {property.Value.EnumType} must be defined in schema");
                            }
                        }
                    }
                }

                base.OnValidateReferences(validate);
            }

            protected override void OnValidateContent(ValidationContext result)
            {
                // check schema id is defined and valid
                if (Schema != null && !String.IsNullOrEmpty(Schema.Id))
                {
                    var regex = "^[a-zA-Z_][a-zA-Z0-9_]*$";
                    Guard.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(Schema.Id, regex), nameof(Schema.Id));


                    foreach (var _class in Schema.Classes)
                    {
                        Guard.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(_class.Key, regex), nameof(_class.Key));

                        foreach (var property in _class.Value.Properties)
                        {
                            if (property.Value.Count.HasValue)
                            {
                                Guard.MustBeGreaterThanOrEqualTo(property.Value.Count.Value, 2, nameof(property.Value.Count));
                            }
                        }
                    }
                }

                foreach (var propertyTexture in PropertyTextures)
                {
                    foreach (var propertyTextureProperty in propertyTexture.Properties)
                    {
                        var texCoord = propertyTextureProperty.Value.TextureCoordinate;
                        var channels = propertyTextureProperty.Value.Channels;
                        var index = propertyTextureProperty.Value.LogicalTextureIndex;
                        Guard.MustBeGreaterThanOrEqualTo(texCoord, 0, nameof(texCoord));
                        Guard.IsTrue(channels.Count > 0, nameof(channels), "Channels must be defined");
                        Guard.IsTrue(index >= 0, nameof(index), "Index must be defined");
                    }
                }

                foreach (var propertyTable in PropertyTables)
                {
                    Guard.IsTrue(propertyTable.ClassName != null, nameof(propertyTable.ClassName), "Class must be defined");
                    Guard.IsTrue(propertyTable.Count > 0, nameof(propertyTable.Count), "Count must be greater than 0");
                    Guard.IsTrue(propertyTable.Properties.Count > 0, nameof(propertyTable.Properties), "Properties must be defined");
                }

                // Check one of schema or schemaUri is defined, but not both
                // Guard.IsFalse(Schema != null && SchemaUri != null, "Schema/SchemaUri", "Schema and SchemaUri cannot both be defined");
                // Guard.IsFalse(Schema == null && SchemaUri == null, "Schema/SchemaUri", "One of Schema and SchemaUri must be defined");

                base.OnValidateContent(result);
            }

            #endregion
        }

        #region structural properties

        /// <remarks>
        /// Use <see cref="EXTStructuralMetadataRoot.AddPropertyTexture"/> to create an instance of this class.
        /// </remarks> 
        public partial class PropertyTexture : IChildOfList<EXTStructuralMetadataRoot>
        {
            #region lifecycle
            public PropertyTexture()
            {
                _properties = new ChildrenDictionary<PropertyTextureProperty, PropertyTexture>(this);
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                return base.GetLogicalChildren()
                    .Concat(_properties.Values);
            }

            #endregion

            #region child properties

            public int LogicalIndex { get; private set; } = -1;

            public EXTStructuralMetadataRoot LogicalParent { get; private set; }

            void IChildOfList<EXTStructuralMetadataRoot>.SetLogicalParent(EXTStructuralMetadataRoot parent, int index)
            {
                LogicalParent = parent;
                LogicalIndex = index;
            }

            #endregion

            #region properties

            public string ClassName
            {
                get => _class;
                set => _class = value;
            }

            public StructuralMetadataClass ClassInstance
            {
                get
                {
                    if (string.IsNullOrEmpty(ClassName)) return null;
                    var root = _GetModelRoot()?.GetExtension<EXTStructuralMetadataRoot>();
                    if (root == null) return null;

                    if (root.TryGetEmbeddedSchema(out var schema))
                    {
                        return schema.Classes[ClassName];
                    }
                    else return null;
                }
                set
                {
                    // Todo: check value is part of this DOM
                    ClassName = value?.LogicalKey;
                }
            }

            public IReadOnlyDictionary<string, PropertyTextureProperty> Properties => _properties;

            #endregion

            #region API

            private ModelRoot _GetModelRoot() => LogicalParent.LogicalParent;

            public PropertyTextureProperty CreateProperty(string key, Texture texture, IReadOnlyList<int> channels = null)
            {
                var property = CreateProperty(key);
                property.Texture = texture;
                if (channels != null) property.Channels = channels;
                return property;
            }

            private PropertyTextureProperty CreateProperty(string key)
            {
                var property = new PropertyTextureProperty();
                _properties[key] = property;
                return property;
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="PropertyTexture.CreateProperty(string)"/> to create an instance of this class.
        /// </remarks> 
        public partial class PropertyTextureProperty : IChildOfDictionary<PropertyTexture>
        {
            #region lifecycle
            public PropertyTextureProperty()
            {
                _channels = new List<int>();
            }

            #endregion

            #region child properties

            public string LogicalKey { get; private set; }

            public PropertyTexture LogicalParent { get; private set; }

            void IChildOfDictionary<PropertyTexture>.SetLogicalParent(PropertyTexture parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region data

            private ModelRoot _GetModelRoot() => LogicalParent.LogicalParent.LogicalParent;

            public IReadOnlyList<int> Channels
            {
                get => _channels;
                set
                {
                    _channels.Clear();
                    _channels.AddRange(value);
                }
            }

            public Schema2.Texture Texture
            {
                get => _GetModelRoot().LogicalTextures[LogicalTextureIndex];
                set
                {
                    Guard.NotNull(value, nameof(value));
                    Guard.MustShareLogicalParent(_GetModelRoot(), nameof(MeshExtMeshFeatureIDTexture), value, nameof(value));

                    LogicalTextureIndex = value.LogicalIndex;
                }
            }

            #endregion            
        }

        /// <remarks>
        /// Use <see cref="EXTStructuralMetadataRoot.AddPropertyAttribute"/> to create an instance of this class.
        /// </remarks> 
        public partial class PropertyAttribute : IChildOfList<EXTStructuralMetadataRoot>
        {
            #region lifecycle
            public PropertyAttribute()
            {
                _properties = new ChildrenDictionary<PropertyAttributeProperty, PropertyAttribute>(this);
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                return base.GetLogicalChildren()
                    .Concat(_properties.Values);
            }

            #endregion

            #region child properties

            /// <summary>
            /// Gets the zero-based index of this <see cref="MeshExtInstanceFeatureID"/> at <see cref="MeshExtInstanceFeatures.FeatureIds"/>.
            /// </summary>
            public int LogicalIndex { get; private set; } = -1;

            /// <summary>
            /// Gets the <see cref="MeshExtInstanceFeatures"/> instance that owns this <see cref="MeshExtInstanceFeatureID"/> instance.
            /// </summary>
            public EXTStructuralMetadataRoot LogicalParent { get; private set; }

            void IChildOfList<EXTStructuralMetadataRoot>.SetLogicalParent(EXTStructuralMetadataRoot parent, int index)
            {
                LogicalParent = parent;
                LogicalIndex = index;
            }

            #endregion

            #region properties
            public string ClassName
            {
                get => _class;
                set => _class = value;
            }

            public StructuralMetadataClass ClassInstance
            {
                get
                {
                    if (string.IsNullOrEmpty(ClassName)) return null;
                    var root = _GetModelRoot()?.GetExtension<EXTStructuralMetadataRoot>();
                    if (root == null) return null;

                    if (root.TryGetEmbeddedSchema(out var schema))
                    {
                        return schema.Classes[ClassName];
                    }
                    else return null;
                }
                set
                {
                    // Todo: check value is part of this DOM
                    ClassName = value?.LogicalKey;
                }
            }

            public IReadOnlyDictionary<string, PropertyAttributeProperty> Properties => _properties;

            #endregion

            #region API

            private ModelRoot _GetModelRoot() => LogicalParent.LogicalParent;

            public PropertyAttributeProperty CreateProperty(string key)
            {
                var property = new PropertyAttributeProperty();
                _properties[key] = property;
                return property;
            }

            #endregion

        }

        /// <remarks>
        /// Use <see cref="PropertyAttribute.CreateProperty(string)"/> to create an instance of this class.
        /// </remarks> 
        public partial class PropertyAttributeProperty : IChildOfDictionary<PropertyAttribute>
        {
            #region child properties

            public string LogicalKey { get; private set; }

            public PropertyAttribute LogicalParent { get; private set; }

            void IChildOfDictionary<PropertyAttribute>.SetLogicalParent(PropertyAttribute parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region properties

            public string Attribute
            {
                get => _attribute;
                set => _attribute = value;
            }

            /** Commented out for now, as it is not supported
            public JsonNode Min
            {
                get => _min;
                set => _min = value;
            }

            public JsonNode Max
            {
                get => _max;
                set => _max = value;
            }

            public JsonNode Scale
            {
                get => _scale;
                set => _scale = value;
            }

            public JsonNode Offset
            {
                get => _offset;
                set => _offset = value;
            }
            */
            #endregion
        }

        /// <remarks>
        /// Use <see cref="EXTStructuralMetadataRoot.AddPropertyTable"/> to create an instance of this class.
        /// </remarks> 
        public partial class PropertyTable : IChildOfList<EXTStructuralMetadataRoot>
        {
            #region lifecycle
            internal PropertyTable()
            {
                _properties = new ChildrenDictionary<PropertyTableProperty, PropertyTable>(this);

                _count = _countMinimum;
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                return base.GetLogicalChildren()
                    .Concat(_properties.Values);
            }

            #endregion

            #region child properties

            public int LogicalIndex { get; private set; } = -1;
            public EXTStructuralMetadataRoot LogicalParent { get; private set; }

            void IChildOfList<EXTStructuralMetadataRoot>.SetLogicalParent(EXTStructuralMetadataRoot parent, int index)
            {
                LogicalParent = parent;
                LogicalIndex = index;
            }

            #endregion

            #region properties

            public IReadOnlyDictionary<string, PropertyTableProperty> Properties => _properties;

            public string ClassName
            {
                get => _class;
                set => _class = value;
            }

            public StructuralMetadataClass ClassInstance
            {
                get
                {
                    if (string.IsNullOrEmpty(ClassName)) return null;
                    var root = _GetModelRoot()?.GetExtension<EXTStructuralMetadataRoot>();
                    if (root == null) return null;

                    if (root.TryGetEmbeddedSchema(out var schema))
                    {
                        return schema.Classes[ClassName];
                    }
                    else return null;
                }
                set
                {
                    // Todo: check value is part of this DOM
                    ClassName = value?.LogicalKey;
                }
            }

            public string Name
            {
                get => _name;
                set => _name = value;
            }

            public int Count
            {
                get => _count;
                set
                {
                    Guard.MustBeGreaterThanOrEqualTo(value, _countMinimum, nameof(value));
                    _count = value;
                }
            }

            #endregion

            #region API

            private ModelRoot _GetModelRoot() => LogicalParent.LogicalParent;

            public PropertyTableProperty UseProperty(StructuralMetadataClassProperty key)
            {
                return UseProperty(key.LogicalKey);
            }

            public PropertyTableProperty UseProperty(string key)
            {
                if (_properties.TryGetValue(key, out var value)) return value;

                value = new PropertyTableProperty();
                _properties[key] = value;
                return value;
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="PropertyTable.UseProperty(string)"/> to create an instance of this class.
        /// </remarks>  
        public partial class PropertyTableProperty : IChildOfDictionary<PropertyTable>
        {
            #region child properties

            public string LogicalKey { get; private set; }

            public PropertyTable LogicalParent { get; private set; }

            void IChildOfDictionary<PropertyTable>.SetLogicalParent(PropertyTable parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region properties

            /// <summary>
            /// this is an index to a BufferView
            /// </summary>
            public int Values
            {
                get => _values;
                set => _values = value;
            }

            public int? ArrayOffsets
            {
                get => _arrayOffsets;
                set => _arrayOffsets = value;
            }

            public int? StringOffsets
            {
                get => _stringOffsets;
                set => _stringOffsets = value;
            }

            #endregion

            #region API

            private ModelRoot _GetModelRoot() => LogicalParent.LogicalParent.LogicalParent;

            public void SetArrayValues<T>(List<List<T>> values)
            {
                Guard.IsTrue(values.Count == LogicalParent.Count, nameof(values), $"Values must have length {LogicalParent.Count}");

                var className = LogicalParent.ClassName;
                var metadataProperty = GetProperty<T>(className, LogicalKey);

                metadataProperty.Array = true;

                var root = _GetModelRoot();

                int logicalIndex = GetBufferView(root, values);
                Values = logicalIndex;

                var hasVariableLength = HasVariableLength<T>(values);

                if (HasVariableLength<T>(values) || typeof(T) == typeof(string))
                {
                    // if the array has items of variable length, create arraysOffsets bufferview
                    var arrayOffsets = BinaryTable.GetArrayOffsets(values);
                    int logicalIndexOffsets = GetBufferView(root, arrayOffsets);
                    ArrayOffsets = logicalIndexOffsets;

                    if (typeof(T) == typeof(string))
                    {
                        var stringValues = values.ConvertAll(x => x.ConvertAll(y => (string)Convert.ChangeType(y, typeof(string), CultureInfo.InvariantCulture)));
                        var stringOffsets = BinaryTable.GetStringOffsets(stringValues);
                        int offsets = GetBufferView(root, stringOffsets);
                        StringOffsets = offsets;
                    }
                }
                else
                {
                    metadataProperty.Count = values[0].Count;
                }
            }

            public void SetValues<T>(params T[] values)
            {
                Guard.IsTrue(values.Length == LogicalParent.Count, nameof(values), $"Values must have length {LogicalParent.Count}");
                var className = LogicalParent.ClassName;
                GetProperty<T>(className, LogicalKey);

                var root = _GetModelRoot();

                int logicalIndex = GetBufferView(root, values);
                Values = logicalIndex;

                if (typeof(T) == typeof(string))
                {
                    var stringvalues = values
                        .Select(x => (string)Convert.ChangeType(x, typeof(string), CultureInfo.InvariantCulture))
                        .ToList();

                    var stringOffsets = BinaryTable.GetStringOffsets(stringvalues);
                    int offsets = GetBufferView(root, stringOffsets);
                    StringOffsets = offsets;
                }
            }

            private StructuralMetadataClassProperty GetProperty<T>(string className, string key)
            {
                var metadataClass = LogicalParent.LogicalParent.Schema.Classes[className];
                Guard.IsTrue(metadataClass != null, nameof(className), $"Schema class {className} must be defined");
                metadataClass.Properties.TryGetValue(key, out var metadataProperty);
                Guard.IsTrue(metadataProperty != null, nameof(key), $"Property {key} in {className} must be defined");

                CheckElementTypes<T>(metadataProperty);

                return metadataProperty;
            }

            private bool HasVariableLength<T>(List<List<T>> values)
            {
                int length = values[0].Count;
                for (int i = 1; i < values.Count; i++)
                {
                    if (values[i].Count != length) return true;
                }
                return false;
            }


            private void CheckElementTypes<T>(StructuralMetadataClassProperty metadataProperty)
            {
                var elementType = metadataProperty.Type;

                if (elementType == ELEMENTTYPE.ENUM)
                {
                    // guard the type of t is an short in case of enum
                    Guard.IsTrue(typeof(T) == typeof(short), nameof(T), $"Enum value type of {LogicalKey} must be short");
                }
                else if (elementType == ELEMENTTYPE.SCALAR)
                {
                    var componentType = metadataProperty.ComponentType;
                    CheckScalarTypes<T>(componentType);
                }
                else if (elementType == ELEMENTTYPE.STRING)
                {
                    Guard.IsTrue(typeof(T) == typeof(string), nameof(T), $"String type of property {LogicalKey} must be string");
                }
                else if (elementType == ELEMENTTYPE.BOOLEAN)
                {
                    Guard.IsTrue(typeof(T) == typeof(bool), nameof(T), $"Boolean type of property {LogicalKey} must beboolean");
                }
                else if (elementType == ELEMENTTYPE.VEC2)
                {
                    Guard.IsTrue(typeof(T) == typeof(Vector2), nameof(T), $"Vector2 type of property {LogicalKey} must be Vector2");
                }
                else if (elementType == ELEMENTTYPE.VEC3)
                {
                    Guard.IsTrue(typeof(T) == typeof(Vector3), nameof(T), $"Vector3 type of property {LogicalKey} must be Vector3");
                }
                else if (elementType == ELEMENTTYPE.VEC3)
                {
                    Guard.IsTrue(typeof(T) == typeof(Vector4), nameof(T), $"Vector4 type of property {LogicalKey} must be Vector4");
                }
                // todo: add MAT2, MAT3
                else if (elementType == ELEMENTTYPE.MAT4)
                {
                    Guard.IsTrue(typeof(T) == typeof(Matrix4x4), nameof(T), $"Matrix4x4 type of property {LogicalKey} must be Matrix4x4");
                }
            }

            private void CheckScalarTypes<T>(DATATYPE? componentType)
            {
                if (componentType == DATATYPE.INT8)
                {
                    Guard.IsTrue(typeof(T) == typeof(sbyte), nameof(T), $"Scalar value type of property {LogicalKey} must be sbyte");
                }
                else if (componentType == DATATYPE.UINT8)
                {
                    Guard.IsTrue(typeof(T) == typeof(byte), nameof(T), $"Scalar value type of property {LogicalKey} must be byte");
                }
                else if (componentType == DATATYPE.INT16)
                {
                    Guard.IsTrue(typeof(T) == typeof(short), nameof(T), $"Scalar value type of property {LogicalKey} must be short");
                }
                else if (componentType == DATATYPE.UINT16)
                {
                    Guard.IsTrue(typeof(T) == typeof(ushort), nameof(T), $"Scalar value type of property {LogicalKey} must be ushort");
                }
                else if (componentType == DATATYPE.INT32)
                {
                    Guard.IsTrue(typeof(T) == typeof(int), nameof(T), $"Scalar value type of property {LogicalKey} must be int");
                }
                else if (componentType == DATATYPE.UINT32)
                {
                    Guard.IsTrue(typeof(T) == typeof(uint), nameof(T), $"Scalar value type of property {LogicalKey} must be uint");
                }
                else if (componentType == DATATYPE.INT64)
                {
                    Guard.IsTrue(typeof(T) == typeof(long), nameof(T), $"Scalar value type of property {LogicalKey} must be long");
                }
                else if (componentType == DATATYPE.UINT64)
                {
                    Guard.IsTrue(typeof(T) == typeof(ulong), nameof(T), $"Scalar value type of property {LogicalKey} must be ulong");
                }
                else if (componentType == DATATYPE.FLOAT32)
                {
                    Guard.IsTrue(typeof(T) == typeof(Single), nameof(T), $"Scalar value type of property {LogicalKey} must be float");
                }
                else if (componentType == DATATYPE.FLOAT64)
                {
                    Guard.IsTrue(typeof(T) == typeof(double), nameof(T), $"Scalar value type of property {LogicalKey} must be double");
                }
            }

            private static int GetBufferView<T>(ModelRoot model, IReadOnlyList<T> values)
            {
                var bytes = BinaryTable.GetBytes(values);
                var bufferView = model.UseBufferView(bytes);
                int logicalIndex = bufferView.LogicalIndex;
                return logicalIndex;
            }

            private static int GetBufferView<T>(ModelRoot model, List<List<T>> values)
            {
                List<byte> bytes = BinaryTable.GetBytesForArray(values);
                var bufferView = model.UseBufferView(bytes.ToArray());
                int logicalIndex = bufferView.LogicalIndex;
                return logicalIndex;
            }

            #endregion
        }

        #endregion

        #region structural schema

        /// <remarks>
        /// Use <see cref="EXTStructuralMetadataRoot.UseEmbeddedSchema()"/> to create an instance of this class.
        /// </remarks>
        public partial class StructuralMetadataSchema : IChildOf<EXTStructuralMetadataRoot>
        {
            #region lifecycle
            public StructuralMetadataSchema()
            {
                _classes = new ChildrenDictionary<StructuralMetadataClass, StructuralMetadataSchema>(this);
                _enums = new ChildrenDictionary<StructuralMetadataEnum, StructuralMetadataSchema>(this);
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                return base.GetLogicalChildren()
                    .Concat(_classes.Values)
                    .Concat(_enums.Values);
            }

            #endregion

            #region child properties           

            public EXTStructuralMetadataRoot LogicalParent { get; private set; }

            void IChildOf<EXTStructuralMetadataRoot>.SetLogicalParent(EXTStructuralMetadataRoot parent)
            {
                LogicalParent = parent;
            }

            #endregion

            #region properties        

            public IReadOnlyDictionary<string, StructuralMetadataClass> Classes => _classes;
            public IReadOnlyDictionary<string, StructuralMetadataEnum> Enums => _enums;

            public string Id
            {
                get => _id;
                set => _id = value;
            }

            public string Version
            {
                get => _version;
                set => _version = value;
            }

            public string Name
            {
                get => _name;
                set => _name = value;
            }

            public string Description
            {
                get => _description;
                set => _description = value;
            }

            #endregion

            #region API

            public StructuralMetadataClass UseClassMetadata(string key)
            {
                if (_classes.TryGetValue(key, out var value)) return value;

                value = new StructuralMetadataClass();
                _classes[key] = value;
                return value;
            }

            public StructuralMetadataEnum UseEnumMetadata(string key, params (string name, int value)[] enumValues)
            {
                var enumType = UseEnumMetadata(key);
                foreach (var (name, value) in enumValues)
                {
                    enumType.AddEnum(name, value);
                }
                return enumType;
            }

            public StructuralMetadataEnum UseEnumMetadata(string key)
            {
                if (_enums.TryGetValue(key, out var value)) return value;

                value = new StructuralMetadataEnum();
                _enums[key] = value;
                return value;
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="StructuralMetadataSchema.UseEnumMetadata(string)"/> to create an instance of this class.
        /// </remarks> 
        public partial class StructuralMetadataEnum : IChildOfDictionary<StructuralMetadataSchema>
        {
            #region lifecycle
            public StructuralMetadataEnum()
            {
                _values = new ChildrenList<StructuralMetadataEnumValue, StructuralMetadataEnum>(this);
            }

            #endregion

            #region child properties

            public string LogicalKey { get; private set; }

            public StructuralMetadataSchema LogicalParent { get; private set; }

            void IChildOfDictionary<StructuralMetadataSchema>.SetLogicalParent(StructuralMetadataSchema parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region properties

            public IReadOnlyList<StructuralMetadataEnumValue> Values => _values;


            public string Name
            {
                get => _name;
                set => _name = value;
            }
            public string Description
            {
                get => _description;
                set => _description = value;
            }

            public IntegerType? ValueType
            {
                get => _valueType;
            }

            #endregion

            #region API

            public StructuralMetadataEnumValue AddEnum(string name, int value, string desc = null)
            {
                var prop = AddEnum();
                prop.Name = name;
                prop.Value = value;
                prop.Description = desc;
                return prop;
            }

            public StructuralMetadataEnumValue AddEnum()
            {
                var prop = new StructuralMetadataEnumValue();
                _values.Add(prop);
                return prop;
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="StructuralMetadataEnum.AddEnum()"/> to create an instance of this class.
        /// </remarks> 
        public partial class StructuralMetadataEnumValue : IChildOfList<StructuralMetadataEnum>
        {
            #region child properties

            public int LogicalIndex { get; private set; } = -1;

            public StructuralMetadataEnum LogicalParent { get; private set; }

            void IChildOfList<StructuralMetadataEnum>.SetLogicalParent(StructuralMetadataEnum parent, int index)
            {
                LogicalParent = parent;
                LogicalIndex = index;
            }

            #endregion

            #region properties
            public string Description
            {
                get => _description;
                set => _description = value;
            }
            public string Name
            {
                get => _name;
                set => _name = value;
            }
            public int Value
            {
                get => _value;
                set => _value = value;
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="StructuralMetadataSchema.UseClassMetadata(string)"/> to create an instance of this class.
        /// </remarks> 
        public partial class StructuralMetadataClass : IChildOfDictionary<StructuralMetadataSchema>
        {
            #region lifecycle

            public StructuralMetadataClass()
            {
                _properties = new ChildrenDictionary<StructuralMetadataClassProperty, StructuralMetadataClass>(this);
            }

            protected override IEnumerable<ExtraProperties> GetLogicalChildren()
            {
                return base.GetLogicalChildren()
                    .Concat(_properties.Values);
            }

            #endregion

            #region child properties

            public string LogicalKey { get; private set; }

            public StructuralMetadataSchema LogicalParent { get; private set; }

            void IChildOfDictionary<StructuralMetadataSchema>.SetLogicalParent(StructuralMetadataSchema parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region properties

            public IReadOnlyDictionary<string, StructuralMetadataClassProperty> Properties => _properties;

            public string Name
            {
                get => _name;
                set => _name = value;
            }

            public string Description
            {
                get => _description;
                set => _description = value;
            }

            #endregion

            #region API

            public StructuralMetadataClass WithName(string name)
            {
                Name = name;
                return this;
            }

            public StructuralMetadataClass WithDescription(string description)
            {
                Description = description;
                return this;
            }

            public StructuralMetadataClassProperty UseProperty(string key)
            {
                if (_properties.TryGetValue(key, out var value)) return value;

                value = new StructuralMetadataClassProperty();
                _properties[key] = value;
                return value;
            }

            public PropertyTexture AddPropertyTexture()
            {
                return LogicalParent.LogicalParent.AddPropertyTexture(this);
            }

            public PropertyAttribute AddPropertyAttribute()
            {
                return LogicalParent.LogicalParent.AddPropertyAttribute(this);
            }

            public PropertyTable AddPropertyTable(int featureCount, string name = null)
            {
                return LogicalParent.LogicalParent.AddPropertyTable(this, featureCount, name);
            }

            #endregion
        }

        /// <remarks>
        /// Use <see cref="StructuralMetadataClass.UseProperty(string)"/> to create an instance of this class.
        /// </remarks> 
        public partial class StructuralMetadataClassProperty : IChildOfDictionary<StructuralMetadataClass>
        {
            #region child properties

            public string LogicalKey { get; private set; }

            public StructuralMetadataClass LogicalParent { get; private set; }

            void IChildOfDictionary<StructuralMetadataClass>.SetLogicalParent(StructuralMetadataClass parent, string key)
            {
                LogicalParent = parent;
                LogicalKey = key;
            }

            #endregion

            #region properties
            public string Name
            {
                get => _name;
                set => _name = value;
            }

            public string Description
            {
                get => _description;
                set => _description = value;
            }

            internal ELEMENTTYPE Type
            {
                get => _type;
                set => _type = value;
            }

            public string EnumType
            {
                get => _enumType;
                // set => _enumType = value;
            }

            public DATATYPE? ComponentType
            {
                get => _componentType;
                set => _componentType = value;
            }

            public bool Required
            {
                get => _required ?? _requiredDefault;
                set => _required = value.AsNullable(_requiredDefault);
            }

            public bool Normalized
            {
                get => _normalized ?? _normalizedDefault;
                set => _normalized = value.AsNullable(_normalizedDefault);
            }

            public bool Array
            {
                get => _array ?? _arrayDefault;
                set => _array = value.AsNullable(_arrayDefault);
            }

            public int? Count
            {
                get => _count;
                set => _count = value;
            }

            /** Commented out for now, as it is not supported
            public JsonNode Min
            {
                get => _min;
                set => _min = value;
            }

            public JsonNode Max
            {
                get => _max;
                set => _max = value;
            }

            public JsonNode Scale
            {
                get => _scale;
                set => _scale = value;
            }

            public JsonNode Offset
            {
                get => _offset;
                set => _offset = value;
            }
            */


            #endregion

            #region API


            public StructuralMetadataClassProperty WithName(string name)
            {
                Name = name;
                return this;
            }

            public StructuralMetadataClassProperty WithDescription(string description)
            {
                Description = description;
                return this;
            }

            public StructuralMetadataClassProperty WithStringType()
            {
                Type = ElementType.STRING;
                return this;
            }

            public StructuralMetadataClassProperty WithBooleanType()
            {
                Type = ElementType.BOOLEAN;
                return this;
            }

            public StructuralMetadataClassProperty WithUInt8Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.UINT8;
                return this;
            }

            public StructuralMetadataClassProperty WithInt8Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.INT8;
                return this;
            }

            public StructuralMetadataClassProperty WithUInt16Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.UINT16;
                return this;
            }

            public StructuralMetadataClassProperty WithInt16Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.INT16;
                return this;
            }

            public StructuralMetadataClassProperty WithUInt32Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.UINT32;
                return this;
            }

            public StructuralMetadataClassProperty WithInt32Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.INT32;
                return this;
            }

            public StructuralMetadataClassProperty WithUInt64Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.UINT64;
                return this;
            }

            public StructuralMetadataClassProperty WithInt64Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.INT64;
                return this;
            }

            public StructuralMetadataClassProperty WithFloat32Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.FLOAT32;
                return this;
            }

            public StructuralMetadataClassProperty WithFloat64Type()
            {
                Type = ELEMENTTYPE.SCALAR;
                ComponentType = DATATYPE.FLOAT64;
                return this;
            }


            public StructuralMetadataClassProperty WithVector3Type()
            {
                Type = ElementType.VEC3;
                ComponentType = DataType.FLOAT32;
                return this;
            }

            public StructuralMetadataClassProperty WithMatrix4x4Type()
            {
                Type = ElementType.MAT4;
                ComponentType = DataType.FLOAT32;
                return this;
            }

            public StructuralMetadataClassProperty WithCount()
            {
                Type = ElementType.MAT4;
                ComponentType = DataType.FLOAT32;
                return this;
            }


            //public StructuralMetadataClassProperty WithValueType(ELEMENTTYPE etype, DATATYPE? ctype = null)
            //{
            //    Type = etype;
            //    ComponentType = ctype;
            //    Array = false;
            //    return this;
            //}

            public StructuralMetadataClassProperty WithArrayType(ELEMENTTYPE etype, DATATYPE? ctype = null, int? count = null)
            {
                Type = etype;
                ComponentType = ctype;
                Array = true;
                Count = count;
                return this;
            }

            public StructuralMetadataClassProperty WithEnumArrayType(StructuralMetadataEnum enumeration, int? count = null)
            {
                Type = ELEMENTTYPE.ENUM;
                _enumType = enumeration.LogicalKey;
                Array = true;
                Count = count;
                return this;
            }

            public StructuralMetadataClassProperty WithEnumeration(StructuralMetadataEnum enumeration)
            {
                Type = ELEMENTTYPE.ENUM;
                _enumType = enumeration.LogicalKey;
                return this;
            }

            public StructuralMetadataClassProperty WithRequired(bool required)
            {
                Required = required;
                return this;
            }

            public StructuralMetadataClassProperty WithNormalized(bool normalized)
            {
                Normalized = normalized;
                return this;
            }


            #endregion
        }

        #endregion
    }
}

