﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SharpGLTF.Materials
{
    using BYTES = ArraySegment<Byte>;

    using TEXLERP = Schema2.TextureInterpolationFilter;
    using TEXMIPMAP = Schema2.TextureMipMapFilter;
    using TEXWRAP = Schema2.TextureWrapMode;

    [System.Diagnostics.DebuggerDisplay("Texture {CoordinateSet} {MinFilter} {MagFilter} {WrapS} {WrapT} {Rotation} {Offset} {Scale}")]
    public class TextureBuilder
    {
        #region lifecycle

        internal TextureBuilder(ChannelBuilder parent)
        {
            Guard.NotNull(parent, nameof(parent));

            _Parent = parent;
        }

        #endregion

        #region data

        private readonly ChannelBuilder _Parent;

        #endregion

        #region properties

        public int CoordinateSet { get; set; } = 0;

        public BYTES PrimaryImageContent { get; set; }

        public BYTES FallbackImageContent { get; set; }

        public TEXMIPMAP MinFilter { get; set; } = TEXMIPMAP.DEFAULT;

        public TEXLERP MagFilter { get; set; } = TEXLERP.DEFAULT;

        public TEXWRAP WrapS { get; set; } = TEXWRAP.REPEAT;

        public TEXWRAP WrapT { get; set; } = TEXWRAP.REPEAT;

        #endregion

        #region API

        public TextureBuilder WithCoordinateSet(int cset) { CoordinateSet = cset; return this; }

        public TextureBuilder WithImage(string primaryImagePath, string fallbackImagePath = null)
        {
            var primary = System.IO.File.ReadAllBytes(primaryImagePath).Slice(0);

            if (string.IsNullOrWhiteSpace(fallbackImagePath)) return WithImage(primary);

            var fallback = System.IO.File.ReadAllBytes(fallbackImagePath).Slice(0);

            return WithImage(primary, fallback);
        }

        public TextureBuilder WithImage(BYTES primary)
        {
            this.PrimaryImageContent = primary;
            this.FallbackImageContent = default;
            return this;
        }

        public TextureBuilder WithImage(BYTES primary, BYTES fallback)
        {
            this.PrimaryImageContent = primary;
            this.FallbackImageContent = fallback;
            return this;
        }

        public TextureBuilder WithSampler(TEXMIPMAP min = TEXMIPMAP.LINEAR, TEXLERP mag = TEXLERP.LINEAR, TEXWRAP ws = TEXWRAP.REPEAT, TEXWRAP wt = TEXWRAP.REPEAT)
        {
            this.MinFilter = min;
            this.MagFilter = mag;
            this.WrapS = ws;
            this.WrapT = wt;

            return this;
        }

        #endregion
    }
}
