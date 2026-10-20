import { useMemo, useState } from "react";
import { Calendar, Clock, UserRound } from "lucide-react";
import { PageHero } from "@/components/PageHero";
import { PrintButton } from "@/components/ListButtons";
import { Notice } from "@/components/Notice";
import { PageLoading } from "@/components/PageState";
import { SearchableSelect } from "@/components/form/SearchableSelect";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useTabParam } from "@/hooks/useTabParam";
import { Permission } from "@/routes/permissions";
import {
  academicYearService,
  type AcademicYearResponse,
} from "@/services/academicYearService";
import { ClassTimetableTab } from "../components/ClassTimetableTab";
import { PeriodsTab } from "../components/PeriodsTab";
import { TeacherTimetableTab } from "../components/TeacherTimetableTab";
import "../timetable.css";

type TabId = "class" | "teacher" | "periods";

/**
 * The timetable: a class's week (with Copy From), a teacher's week, and the periods of the day.
 * What shows depends on the role: Timetable.View sees everything, Timetable.Edit changes lessons,
 * Timetable.ManagePeriods changes periods, and Timetable.ViewScoped (a teacher) sees only their own week.
 */
export function TimetablePage() {
  const can = useCan();
  const canView = can(Permission.TimetableView);
  const canEdit = can(Permission.TimetableEdit);
  const canManage = can(Permission.TimetableManagePeriods);

  // The class and teacher lists come from other modules' endpoints, so those tabs also need
  // permission to read them (the default roles have it).
  const showClass = canView && can(Permission.ClassSectionsView);
  const showTeacher = canView
    ? can(Permission.TeachersView)
    : can(Permission.TimetableViewScoped);

  const tabs: (TabItem & { id: TabId })[] = [
    ...(showClass
      ? [{ id: "class" as const, label: "Class timetable", icon: Calendar }]
      : []),
    ...(showTeacher
      ? [
          {
            id: "teacher" as const,
            label: canView ? "Teacher timetable" : "My timetable",
            icon: UserRound,
          },
        ]
      : []),
    ...(canView
      ? [{ id: "periods" as const, label: "Periods", icon: Clock }]
      : []),
  ];
  const [active, selectTab] = useTabParam(tabs.map((t) => t.id));

  // The academic year picker is only for users who may list years (Timetable.View).
  const years = useAsyncData(
    () =>
      canView
        ? academicYearService.getAll()
        : Promise.resolve([] as AcademicYearResponse[]),
    [canView],
    "Could not load academic years.",
  );
  const classSections = useAsyncData(
    () =>
      showClass
        ? classSectionService.getAll()
        : Promise.resolve([] as ClassSectionResponse[]),
    [showClass],
    "Could not load classes.",
  );

  const [pickedYear, setPickedYear] = useState<number | null>(null);
  const yearList = useMemo(() => years.data ?? [], [years.data]);
  const yearId = yearList.some((y) => y.id === pickedYear)
    ? pickedYear
    : (yearList.find((y) => y.isCurrent)?.id ?? yearList[0]?.id ?? null);

  if (tabs.length === 0)
    return (
      <div>
        <PageHero title="Timetable" />
        <Notice tone="warn">You don't have access to any timetable view.</Notice>
      </div>
    );

  if (
    (canView && years.isLoading && !years.data) ||
    (showClass && classSections.isLoading && !classSections.data)
  )
    return <PageLoading />;

  return (
    <div>
      <PageHero
        title="Timetable"
        subtitle={
          canView
            ? "Weekly timetables of classes and teachers, and the periods of the school day."
            : "Your weekly timetable."
        }
        actions={<PrintButton />}
      />

      {years.error && <Notice tone="warn">{years.error}</Notice>}

      {canView && (
        <div className="card" style={{ marginBottom: 18 }}>
          <div className="filters">
            <div className="field">
              <label htmlFor="tt-year">Academic year</label>
              <SearchableSelect
                id="tt-year"
                options={yearList.map((y) => ({
                  value: y.id,
                  label: y.isCurrent ? `${y.name} (current)` : y.name,
                }))}
                value={yearId ?? ""}
                onChange={(v) => setPickedYear(Number(v))}
              />
            </div>
          </div>
        </div>
      )}

      <Tabs
        idBase="tt"
        label="Timetable views"
        tabs={tabs}
        active={active}
        onChange={selectTab}
      />
      <TabPanel idBase="tt" id={active}>
        {active === "class" && (
          <ClassTimetableTab
            yearId={yearId}
            classSections={classSections.data ?? []}
            canEdit={canEdit}
          />
        )}
        {active === "teacher" && (
          <TeacherTimetableTab yearId={yearId} ownOnly={!canView} />
        )}
        {active === "periods" && (
          <PeriodsTab yearId={yearId} canManage={canManage} />
        )}
      </TabPanel>
    </div>
  );
}
