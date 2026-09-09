using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Clio.Desktop
{
    internal sealed class StoryChoiceRecord
    {
        internal readonly int Turn;
        internal readonly string Title, Choice, Consequences;
        internal StoryChoiceRecord(int turn, string title, string choice, string consequences)
        { Turn = turn; Title = title; Choice = choice; Consequences = consequences; }
    }

    internal sealed class StoryModeSaveState
    {
        internal bool Semiautomatic;
        internal StoryDirective Directive;
        internal int UntilTurn;
        internal string PreviousEventKey = "";
        internal readonly List<StoryChoiceRecord> Choices = new List<StoryChoiceRecord>();
    }

    public sealed partial class GameForm
    {
        // Presentation choices accompany the ordinary command replay. No part of
        // this metadata is interpreted as a command or changes replayed outcomes.
        private const int MaximumStoryChoices = 256;
        private const int MaximumStoryMetadataBytes = 1048576;
        private static readonly Encoding StoryMetadataEncoding = new UTF8Encoding(false, true);

        private bool HasStoryModeSave
        { get { return semiautomatic || storyDirectiveUntilTurn > 0 || storyChoices.Count > 0 || !String.IsNullOrEmpty(storyPreviousEventKey); } }

        private string SerializeStoryModeSave()
        {
            StoryModeSaveState state = new StoryModeSaveState
            {
                Semiautomatic = semiautomatic,
                Directive = storyDirective,
                UntilTurn = storyDirectiveUntilTurn,
                PreviousEventKey = storyPreviousEventKey ?? ""
            };
            int first = Math.Max(0, storyChoices.Count - MaximumStoryChoices);
            for (int i = first; i < storyChoices.Count; i++) state.Choices.Add(storyChoices[i]);
            ValidateStoryModeSave(state, game.Turn);
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, StoryMetadataEncoding))
                {
                    writer.Write(1); // Compact metadata schema, separate from the story version.
                    writer.Write((byte)(state.Semiautomatic ? 1 : 0));
                    writer.Write((int)state.Directive);
                    writer.Write(state.UntilTurn);
                    WriteStoryMetadataText(writer, state.PreviousEventKey, 128);
                    writer.Write(state.Choices.Count);
                    foreach (StoryChoiceRecord choice in state.Choices)
                    {
                        writer.Write(choice.Turn);
                        WriteStoryMetadataText(writer, choice.Title, 256);
                        WriteStoryMetadataText(writer, choice.Choice, 256);
                        WriteStoryMetadataText(writer, choice.Consequences, 2048);
                    }
                    writer.Flush();
                    if (stream.Length > MaximumStoryMetadataBytes) throw new InvalidDataException("The decision record is too large to save.");
                    return Convert.ToBase64String(stream.ToArray());
                }
            }
        }

        private static StoryModeSaveState ParseStoryModeSave(string encoded)
        {
            if (String.IsNullOrEmpty(encoded) || encoded.Length > (MaximumStoryMetadataBytes + 2) / 3 * 4)
                throw new InvalidDataException("Invalid decision record size.");
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                if (bytes.Length > MaximumStoryMetadataBytes) throw new InvalidDataException("The decision record is too large.");
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (BinaryReader reader = new BinaryReader(stream, StoryMetadataEncoding))
                {
                    if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported decision record version.");
                    byte mode = reader.ReadByte();
                    if (mode > 1) throw new InvalidDataException("Unknown gameplay mode.");
                    StoryModeSaveState state = new StoryModeSaveState
                    {
                        Semiautomatic = mode == 1,
                        Directive = (StoryDirective)reader.ReadInt32(),
                        UntilTurn = reader.ReadInt32(),
                        PreviousEventKey = ReadStoryMetadataText(reader, 128)
                    };
                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaximumStoryChoices) throw new InvalidDataException("Invalid number of recorded decisions.");
                    for (int i = 0; i < count; i++)
                        state.Choices.Add(new StoryChoiceRecord(reader.ReadInt32(), ReadStoryMetadataText(reader, 256),
                            ReadStoryMetadataText(reader, 256), ReadStoryMetadataText(reader, 2048)));
                    if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data after the decision record.");
                    ValidateStoryModeSave(state, MaximumCommands + 1);
                    return state;
                }
            }
            catch (FormatException error) { throw new InvalidDataException("Invalid decision record encoding.", error); }
            catch (EndOfStreamException error) { throw new InvalidDataException("The decision record is incomplete.", error); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid text in the decision record.", error); }
        }

        private static void ValidateStoryModeSave(StoryModeSaveState state, int restoredTurn)
        {
            if (!Enum.IsDefined(typeof(StoryDirective), state.Directive)) throw new InvalidDataException("Unknown decision directive.");
            if (state.UntilTurn < 0 || state.UntilTurn > restoredTurn + 64)
                throw new InvalidDataException("The decision's duration is outside the supported range.");
            ValidateStoryMetadataText(state.PreviousEventKey, 128);
            if (state.Choices.Count > MaximumStoryChoices) throw new InvalidDataException("Too many recorded decisions.");
            int previousTurn = 0;
            foreach (StoryChoiceRecord choice in state.Choices)
            {
                if (choice == null || choice.Turn < 1 || choice.Turn < previousTurn || choice.Turn > restoredTurn)
                    throw new InvalidDataException("The decision record does not match the story's turn.");
                ValidateStoryMetadataText(choice.Title, 256);
                ValidateStoryMetadataText(choice.Choice, 256);
                ValidateStoryMetadataText(choice.Consequences, 2048);
                previousTurn = choice.Turn;
            }
        }

        private void ApplyStoryModeSave(StoryModeSaveState state)
        {
            semiautomatic = state.Semiautomatic;
            storyDirective = state.Directive;
            storyDirectiveUntilTurn = state.UntilTurn;
            storyPreviousEventKey = state.PreviousEventKey;
            storyChoices.Clear();
            storyChoices.AddRange(state.Choices);
        }

        private static void ValidateStoryMetadataText(string value, int maximum)
        {
            if (value == null || value.Length > maximum) throw new InvalidDataException("Invalid decision text length.");
            foreach (char character in value)
                if (Char.IsControl(character) && character != '\r' && character != '\n' && character != '\t')
                    throw new InvalidDataException("Unsupported control character in decision text.");
        }

        private static void WriteStoryMetadataText(BinaryWriter writer, string value, int maximum)
        {
            ValidateStoryMetadataText(value, maximum);
            byte[] bytes = StoryMetadataEncoding.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadStoryMetadataText(BinaryReader reader, int maximum)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > maximum * 4 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid decision text size.");
            string value = StoryMetadataEncoding.GetString(reader.ReadBytes(length));
            ValidateStoryMetadataText(value, maximum);
            return value;
        }
    }
}
