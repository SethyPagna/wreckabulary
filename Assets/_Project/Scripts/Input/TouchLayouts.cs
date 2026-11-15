using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class TouchLayouts
    {
        public static string Folder;
        static TouchLayoutBook book;

        public static event Action Changed;
        public static int Version { get; private set; }
        public static string LoadProblem { get; private set; }
        public static string FolderPath => Folder ?? Path.Combine(Application.persistentDataPath, "touch");
        public static bool Available => Application.isMobilePlatform || Touchscreen.current != null || TouchBinding.Shared.Enabled;

        public static TouchLayoutBook Book
        {
            get
            {
                if (book != null) return book;
                book = TouchLayoutBook.Load(FolderPath, out string problem);
                LoadProblem = problem;
                return book;
            }
        }

        public static TouchLayout Current(Vector2 size) => Book.Current(size.x, size.y);

        public static bool Select(TouchTemplate template, out string error)
        {
            var next = Book.Clone();
            next.Active = template;
            return Commit(next, out error);
        }

        public static bool Commit(TouchLayoutBook next, out string error)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            bool saved = next.TrySave(FolderPath, out error);
            book = next.Clone();
            Version++;
            Changed?.Invoke();
            return saved;
        }

        public static void Reload()
        {
            book = null;
            Version++;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Folder = null;
            book = null;
            Changed = null;
            LoadProblem = null;
            Version = 0;
        }
    }
}
