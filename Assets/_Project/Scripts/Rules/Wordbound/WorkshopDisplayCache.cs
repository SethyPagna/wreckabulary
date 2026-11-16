using System;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopDisplayCache
    {
        readonly Func<string, CourseDocument> parse;
        string courseJson;
        CourseCatalogue catalogue;
        CoursePlayRequirements play;
        public CourseDocument Document { get; private set; }
        public CourseValidation Validation { get; private set; }
        public int[] IssueTabs { get; private set; } = new int[WorkshopIssueTabs.Tabs];

        public WorkshopDisplayCache(Func<string, CourseDocument> parse = null) =>
            this.parse = parse ?? (json => CourseJson.ImportDocument(json).Document);

        public bool Update(WorkshopSnapshot snapshot, CourseCatalogue approved, CoursePlayRequirements requirements)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            bool same = Holds(snapshot);
            if (same && ReferenceEquals(approved, catalogue) && ReferenceEquals(requirements, play)) return false;
            if (!same) { Document = parse(snapshot.CourseJsonText); courseJson = snapshot.CourseJsonText; }
            catalogue = approved; play = requirements;
            Validation = CourseValidator.Validate(Document, approved, requirements);
            IssueTabs = WorkshopIssueTabs.Counts(Validation.Issues, Document);
            return true;
        }

        public bool Holds(WorkshopSnapshot snapshot) => Document != null && snapshot != null &&
            (ReferenceEquals(snapshot.CourseJsonText, courseJson) || snapshot.CourseJsonText == courseJson);

        public CourseDocument For(WorkshopSnapshot snapshot) =>
            Holds(snapshot) ? Document : parse((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).CourseJsonText);
    }
}
