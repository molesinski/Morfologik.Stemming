﻿using J2N.Collections;
using Lucene.Net.Support;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Morfologik.Stemming
{
    /// <summary>
    /// Description of attributes, their types and default values.
    /// </summary>
    public sealed class DictionaryMetadata
    {
        /// <summary>
        /// Default attribute values.
        /// </summary>
        private static readonly IDictionary<DictionaryAttribute, string> DefaultAttributes = new DictionaryMetadataBuilder()
            .FrequencyIncluded(false)
            .IgnorePunctuation()
            .IgnoreNumbers()
            .IgnoreCamelCase()
            .IgnoreAllUppercase()
            .IgnoreDiacritics()
            .ConvertCase()
            .SupportRunOnWords()
            .ToDictionary();

        /// <summary>
        /// Required attributes.
        /// </summary>
        private static readonly ISet<DictionaryAttribute> RequiredAttributes = new HashSet<DictionaryAttribute>(
            new DictionaryAttribute[] { DictionaryAttribute.Separator,
            DictionaryAttribute.Encoder,
            DictionaryAttribute.Encoding });

        /// <summary>
        /// A separator character between fields (stem, lemma, form). The character
        /// must be within byte range (FSA uses bytes internally).
        /// </summary>
        private byte separator;
        private char separatorChar;

        /// <summary>
        /// Encoding used for converting bytes to characters and vice versa.
        /// </summary>
        private string encoding;

        private Encoding charset;
        private CultureInfo locale = CultureInfo.CurrentCulture;

        /// <summary>
        /// Replacement pairs for non-obvious candidate search in a speller dictionary.
        /// </summary>
        private LinkedHashMap<string, IList<string>> replacementPairs = new LinkedHashMap<string, IList<string>>();

        /// <summary>
        /// Conversion pairs for input conversion, for example to replace ligatures.
        /// </summary>
        private LinkedHashMap<string, string> inputConversion = new LinkedHashMap<string, string>();

        /// <summary>
        /// Conversion pairs for output conversion, for example to replace ligatures.
        /// </summary>
        private LinkedHashMap<string, string> outputConversion = new LinkedHashMap<string, string>();

        /// <summary>
        /// Equivalent characters (treated similarly as equivalent chars with and without
        /// diacritics). For example, Polish <c>ł</c> can be specified as equivalent to <c>l</c>.
        /// This implements a feature similar to hunspell MAP in the affix file.
        /// </summary>
        private LinkedHashMap<char, IList<char>> equivalentChars = new LinkedHashMap<char, IList<char>>();

        /// <summary>
        /// All attributes.
        /// </summary>
        private readonly IDictionary<DictionaryAttribute, string> attributes;

        /// <summary>
        /// All "enabled" boolean attributes.
        /// </summary>
        private readonly IDictionary<DictionaryAttribute, bool> boolAttributes;

        /// <summary>
        /// Sequence encoder.
        /// </summary>
        private EncoderType encoderType;

        /// <summary>
        /// Expected metadata file extension.
        /// </summary>
        public const string MetadataFileExtension = "info";

        /// <summary>
        /// Gets all metadata attributes.
        /// </summary>
        public IDictionary<DictionaryAttribute, string> Attributes => attributes.ToUnmodifiableDictionary();

        // Cached attrs.
        public string Encoding => encoding;
        public byte Separator => separator;
        public CultureInfo Culture => locale; // TODO: We probably want to pass this in rather than store it as a field

        public LinkedHashMap<string, string> InputConversionPairs => inputConversion;
        public LinkedHashMap<string, string> OutputConversionPairs => outputConversion;

        public LinkedHashMap<string, IList<string>> ReplacementPairs => replacementPairs;
        public LinkedHashMap<char, IList<char>> EquivalentChars => equivalentChars;

        // Dynamically fetched.
        public bool IsFrequencyIncluded => boolAttributes[DictionaryAttribute.FrequencyIncluded];
        public bool IsIgnoringPunctuation => boolAttributes[DictionaryAttribute.IgnorePunctuation];
        public bool IsIgnoringNumbers => boolAttributes[DictionaryAttribute.IgnoreNumbers];
        public bool IsIgnoringCamelCase => boolAttributes[DictionaryAttribute.IgnoreCamelCase];
        public bool IsIgnoringAllUppercase => boolAttributes[DictionaryAttribute.IgnoreAllUpperCase];
        public bool IsIgnoringDiacritics => boolAttributes[DictionaryAttribute.IgnoreDiacritics];
        public bool IsConvertingCase => boolAttributes[DictionaryAttribute.ConvertCase];
        public bool IsSupportingRunOnWords => boolAttributes[DictionaryAttribute.RunOnWords];

        /// <summary>
        /// Create an instance from an attribute dictionary.
        /// </summary>
        /// <param name="attrs">A set of <see cref="DictionaryAttribute"/> keys and their associated values.</param>
        /// <seealso cref="DictionaryMetadataBuilder"/>
        public DictionaryMetadata(IDictionary<DictionaryAttribute, string> attrs)
        {
            this.boolAttributes = new Dictionary<DictionaryAttribute, bool>();
            this.attributes = new Dictionary<DictionaryAttribute, string>(attrs);

            IDictionary<DictionaryAttribute, string> attributeMap = new Dictionary<DictionaryAttribute, string>(DefaultAttributes.Union(attrs).ToDictionary(k => k.Key, v => v.Value));

            // Convert some attrs from the map to local fields for performance reasons.
            ISet<DictionaryAttribute> requiredAttributes = new HashSet<DictionaryAttribute>(RequiredAttributes);

            foreach (var e in attributeMap)
            {
                requiredAttributes.Remove(e.Key);

                // Run validation and conversion on all of them.
                object value = e.Key.FromString(e.Value);
                switch (e.Key)
                {
                    case DictionaryAttribute.Encoding:
                        this.encoding = e.Value;
                        this.charset = (Encoding)value;
                        break;

                    case DictionaryAttribute.Separator:
                        this.separatorChar = (char)value;
                        break;

                    case DictionaryAttribute.Culture:
                        this.locale = (CultureInfo)value;
                        break;

                    case DictionaryAttribute.Encoder:
                        this.encoderType = (EncoderType)value;
                        break;

                    case DictionaryAttribute.InputConversion:
                        {
                            LinkedHashMap<string, string> gvalue = (LinkedHashMap<string, string>)value;
                            this.inputConversion = gvalue;
                        }
                        break;

                    case DictionaryAttribute.OutputConversion:
                        {
                            LinkedHashMap<string, string> gvalue = (LinkedHashMap<string, string>)value;
                            this.outputConversion = gvalue;
                        }
                        break;

                    case DictionaryAttribute.ReplacementPairs:
                        {
                            LinkedHashMap<string, IList<string>> gvalue = (LinkedHashMap<string, IList<string>>)value;
                            this.replacementPairs = gvalue;
                        }
                        break;

                    case DictionaryAttribute.EquivalentChars:
                        {
                            LinkedHashMap<char, IList<char>> gvalue = (LinkedHashMap<char, IList<char>>)value;
                            this.equivalentChars = gvalue;
                        }
                        break;

                    case DictionaryAttribute.IgnorePunctuation:
                    case DictionaryAttribute.IgnoreNumbers:
                    case DictionaryAttribute.IgnoreCamelCase:
                    case DictionaryAttribute.IgnoreAllUpperCase:
                    case DictionaryAttribute.IgnoreDiacritics:
                    case DictionaryAttribute.ConvertCase:
                    case DictionaryAttribute.RunOnWords:
                    case DictionaryAttribute.FrequencyIncluded:
                        this.boolAttributes.Put(e.Key, (bool)value);
                        break;

                    case DictionaryAttribute.Author:
                    case DictionaryAttribute.License:
                    case DictionaryAttribute.CreationDate:
                        // Just run validation.
                        e.Key.FromString(e.Value);
                        break;

                    default:
                        throw new Exception("Unexpected code path (attribute should be handled but is not): " + e.Key);
                }
            }

            if (requiredAttributes.Any())
            {
                throw new ArgumentException("At least one the required attributes was not provided: "
                    + CollectionUtil.ToString(requiredAttributes));
            }

            // Sanity check.
            Encoding encoder = Encoder;
                var encoded = encoder.GetBytes(new char[] { separatorChar });
            if (encoded.Length > 1)
            {
                throw new ArgumentException("Separator character is not a single byte in encoding "
                    + encoding + ": " + separatorChar);
            }
            this.separator = encoded[0];
        }

        /// <summary>
        /// Gets a new <see cref="System.Text.Encoding"/> for the <see cref="Encoding"/>. 
        /// </summary>
        public Encoding Decoder => charset;

        /// <summary>
        /// Gets a new <see cref="System.Text.Encoding"/> for the <see cref="Encoding"/>.
        /// </summary>
        public Encoding Encoder => charset;

        /// <summary>
        /// Gets the sequence encoder type.
        /// </summary>
        public EncoderType SequenceEncoderType => encoderType;

        /// <summary>
        /// Returns the <see cref="Separator"/> byte converted to a single
        /// <see cref="char"/>.
        /// </summary>
        /// <exception cref="Exception">If this conversion is for some reason impossible (the byte is a
        /// surrogate pair, FSA's <see cref="Encoding"/> is not available).</exception>
        public char SeparatorAsChar => separatorChar;

        /// <summary>
        /// A shortcut returning <see cref="DictionaryMetadataBuilder"/>.
        /// </summary>
        public static DictionaryMetadataBuilder Builder()
        {
            return new DictionaryMetadataBuilder();
        }

        /// <summary>
        /// Returns the expected name of the metadata file, based on the name of the
        /// dictionary file. The expected name is resolved by truncating any
        /// file extension of <paramref name="dictionaryFile"/> and appending
        /// <see cref="DictionaryMetadata.MetadataFileExtension"/>.
        /// </summary>
        /// <param name="dictionaryFile">The name of the dictionary (<c>*.dict</c>) file. </param>
        /// <returns>Returns the expected name of the metadata file.</returns>
        public static string GetExpectedMetadataFileName(string dictionaryFile)
        {
            int dotIndex = dictionaryFile.LastIndexOf('.');
            string featuresName;
            if (dotIndex >= 0)
            {
                featuresName = dictionaryFile.Substring(0, dotIndex - 0) + "." + MetadataFileExtension; // Corrected 2nd parameter
            }
            else
            {
                featuresName = dictionaryFile + "." + MetadataFileExtension;
            }

            return featuresName;
        }

        /// <summary>
        /// Returns the expected path of a metadata file.
        /// </summary>
        /// <param name="dictionary">The path of the dictionary file.</param>
        /// <returns>Returns the expected path of a metadata file.</returns>
        public static string GetExpectedMetadataLocation(string dictionary)
        {
            return Path.Combine(new FileInfo(dictionary).DirectoryName, GetExpectedMetadataFileName(dictionary));
        }

        /// <summary>
        /// Read dictionary metadata from a property file (stream).
        /// </summary>
        /// <param name="metadataStream">The stream with metadata.</param>
        /// <returns>Returns <see cref="DictionaryMetadata"/> read from a the stream (property file).</returns>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public static DictionaryMetadata Read(Stream metadataStream)
        {
            IDictionary<DictionaryAttribute, string> map = new Dictionary<DictionaryAttribute, string>();
            var properties = new Dictionary<string, string>();
            properties.Load(new StreamReader(metadataStream, System.Text.Encoding.UTF8));

            // Handle back-compatibility for encoder specification.
            if (!properties.ContainsKey(DictionaryAttributeExtensions.Encoder.PropertyName))
            {
                bool hasDeprecated = properties.ContainsKey("fsa.dict.uses-suffixes") ||
                                        properties.ContainsKey("fsa.dict.uses-infixes") ||
                                        properties.ContainsKey("fsa.dict.uses-prefixes");

                bool usesSuffixes = Boolean.Parse(properties.GetProperty("fsa.dict.uses-suffixes", "true")); // TODO: Possible parse exception here
                bool usesPrefixes = Boolean.Parse(properties.GetProperty("fsa.dict.uses-prefixes", "false")); // TODO: Possible parse exception here
                bool usesInfixes = Boolean.Parse(properties.GetProperty("fsa.dict.uses-infixes", "false")); // TODO: Possible parse exception here

                EncoderType encoder;
                if (usesInfixes)
                {
                    encoder = EncoderType.Infix;
                }
                else if (usesPrefixes)
                {
                    encoder = EncoderType.Prefix;
                }
                else if (usesSuffixes)
                {
                    encoder = EncoderType.Suffix;
                }
                else
                {
                    encoder = EncoderType.None;
                }

                if (!hasDeprecated)
                {
                    throw new IOException("Use an explicit " +
                        DictionaryAttributeExtensions.Encoder.PropertyName + "=" + encoder.ToString() +
                        " metadata key: ");
                }

                throw new IOException("Deprecated encoder keys in metadata. Use " +
                    DictionaryAttributeExtensions.Encoder.PropertyName + "=" + encoder.ToString());
            }

            foreach (var prop in properties)
            {
                map[DictionaryAttributeExtensions.FromPropertyName(prop.Key)] = prop.Value;
            }

            return new DictionaryMetadata(map);
        }

        /// <summary>
        /// Write dictionary attributes (metadata).
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public void Write(TextWriter writer)
        {
            var properties = new Dictionary<string, string>();

            foreach (var e in Attributes)
            {
                properties[e.Key.GetPropertyName()] = e.Value;
            }

            properties.Store(writer, "# " + GetType().Name);
        }

        static DictionaryMetadata()
        {
#if NETSTANDARD
            // Support for iso-8859-1 encoding. See: https://docs.microsoft.com/en-us/dotnet/api/system.text.codepagesencodingprovider?view=netcore-2.0
            var encodingProvider = CodePagesEncodingProvider.Instance;
            System.Text.Encoding.RegisterProvider(encodingProvider);
#endif
        }
    }
}