using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("Cubic curve keeps exact endpoints", CurveEndpointsAreExact);
            Run("Cubic curve midpoint is exact", CurveMidpointIsExact);
            Run("Cubic curve sampling is deterministic", CurveSamplingIsDeterministic);
            Run("Invalid curve points are rejected", InvalidCurvePointsAreRejected);
            Run("Default curve remains finite", DefaultCurveRemainsFinite);
            Run("Precision movement quantizes symmetrically", PrecisionMovementQuantizesSymmetrically);
            Run("Precision angles normalize consistently", PrecisionAnglesNormalizeConsistently);
            Run("Precision step presets stay shared with F9", PrecisionStepPresetsStaySharedWithF9);
            Run("Rotation drag follows both camera sides", RotationDragFollowsBothCameraSides);
            Run("Incremental turn follows row direction", IncrementalTurnFollowsRowDirection);
            Run("Invalid precision values are rejected", InvalidPrecisionValuesAreRejected);
            Run("Precision offset limit is symmetric", PrecisionOffsetLimitIsSymmetric);
            Run("Anchor points have exact opposites", AnchorPointsHaveExactOpposites);
            Run("Anchor bounds combine multiple meshes", AnchorBoundsCombineMultipleMeshes);
            Run("Anchor edges use exact midpoints", AnchorEdgesUseExactMidpoints);
            Run("Center anchor matches bounds center", CenterAnchorMatchesBoundsCenter);
            Run("Anchored rotation preserves fixed corner", AnchoredRotationPreservesFixedCorner);
            Run("Centered rotation preserves bounds center", CenteredRotationPreservesBoundsCenter);
            Run("Magnetic movement lands source anchor", MagneticMovementLandsSourceAnchor);
            Run("Anchor focus compensation is exact", AnchorFocusCompensationIsExact);
            Run("Constrained anchor rotation uses selected axis", ConstrainedAnchorRotationUsesSelectedAxis);
            Run("Undo history keeps newest twenty actions", UndoHistoryKeepsNewestTwentyActions);
            Run("Undo history restores redone actions", UndoHistoryRestoresRedoneActions);
            Run("New edit discards redo branch", NewEditDiscardsRedoBranch);
            Run("Snap neighbors add edge middles without rectangle diagonals", SnapNeighborsExcludeRectangleDiagonals);
            Run("Snap neighbors split a line at existing points", SnapNeighborsSplitExistingLine);
            Run("Feature edges exclude flat triangulation", FeatureEdgesExcludeFlatTriangulation);
            Run("Feature edges keep visible creases", FeatureEdgesKeepVisibleCreases);
            Run("Feature edges weld duplicated seams", FeatureEdgesWeldDuplicatedSeams);
            Run("Non-manifold edges remain snap targets", NonManifoldEdgesRemainSnapTargets);
            Run("Screen edge selection is perspective correct", ScreenEdgeSelectionIsPerspectiveCorrect);
            Run("Composite snap points exclude internal joins", CompositeSnapPointsExcludeInternalJoins);
            Run("Composite snap points keep same-part neighbors", CompositeSnapPointsKeepSamePartNeighbors);
            Run("Straight repeat uses exact step", StraightRepeatUsesExactStep);
            Run("Connected ring starts at selected support", ConnectedRingStartsAtSelectedSupport);
            Run("Concave ring follows graph neighbors", ConcaveRingFollowsGraphNeighbors);
            Run("Connected contour accepts open chains", ConnectedContourAcceptsOpenChains);
            Run("Connected contour ignores branches outside a ring", ConnectedContourIgnoresRingBranches);
            Run("Connected contour rejects forked open chains", ConnectedContourRejectsForkedOpenChains);
            Run("Connected contour does not merge nearby unconnected rows", ConnectedContourDoesNotMergeNearbyRows);
            Run("Connected contour accepts a seventy-two piece ring", ConnectedContourAcceptsSeventyTwoPieceRing);
            Run("Touching contour accepts overlapping ring edges", TouchingContourAcceptsOverlappingRingEdges);
            Run("Touching contour keeps overlapping nearby rows separate", TouchingContourKeepsNearbyRowsSeparate);
            Run("Connected contour rejects invalid input", ConnectedContourRejectsInvalidInput);
            Run("Line to line samples matching fractions", LineToLineSamplesMatchingFractions);
            Run("Line to point keeps one aim", LineToPointKeepsOneAim);
            Run("Point to line keeps one contact", PointToLineKeepsOneContact);
            Run("Invalid straight guides are rejected", InvalidStraightGuidesAreRejected);
            Run("Polyline samples by traveled distance", PolylineSamplesByTraveledDistance);
            Run("Arc passes through control point", ArcPassesThroughControlPoint);
            Run("Bezier samples 3D curve by traveled distance", BezierSamples3DCurveByTraveledDistance);
            Run("Repeat modifiers and symmetry are deterministic", RepeatModifiersAndSymmetryAreDeterministic);
            Run("Repeat bends into a vertical arch", RepeatBendsIntoVerticalArch);
            Run("Repeat rolls without changing its straight path", RepeatRollsWithoutChangingStraightPath);
            Run("Repeat accepts arbitrary fractional angles", RepeatAcceptsArbitraryFractionalAngles);
            Run("Repeat rejects unsafe modifiers and limits", RepeatRejectsUnsafeModifiersAndLimits);
            Run("Plane samples both axes", PlaneSamplesBothAxes);
            Run("Plane rejects unsafe axes and copy counts", PlaneRejectsUnsafeAxesAndCopyCounts);
            Run("Guide samples pair points and paths", GuideSamplesPairPointsAndPaths);
            Run("Guide pairing rejects mismatches and collisions", GuidePairingRejectsMismatchesAndCollisions);
            Run("Invalid anchor values are rejected", InvalidAnchorValuesAreRejected);
            Run("Blueprint document deep copies source", BlueprintDocumentDeepCopiesSource);
            Run("Blueprint selection does not dirty document", BlueprintSelectionDoesNotDirtyDocument);
            Run("Blueprint range selection follows tree order", BlueprintRangeSelectionFollowsTreeOrder);
            Run("Blueprint range selection stays inside parent", BlueprintRangeSelectionStaysInsideParent);
            Run("Blueprint groups inherit visibility and lock", BlueprintGroupsInheritVisibilityAndLock);
            Run("Blueprint nested groups share selection and state", BlueprintNestedGroupsShareSelectionAndState);
            Run("Blueprint nested groups move without cycles", BlueprintNestedGroupsMoveWithoutCycles);
            Run("Blueprint group drop is atomic and preserves transforms", BlueprintGroupDropIsAtomic);
            Run("Blueprint outliner commands are atomic", BlueprintOutlinerCommandsAreAtomic);
            Run("Blueprint visibility isolation is atomic", BlueprintVisibilityIsolationIsAtomic);
            Run("Blueprint group transform applies each part once", BlueprintGroupTransformAppliesEachPartOnce);
            Run("Blueprint nested group duplicate is independent", BlueprintNestedDuplicateIsIndependent);
            Run("Blueprint copy drag transforms hidden children and undoes once", BlueprintCopyDragIsAtomic);
            Run("Blueprint insertion remaps hierarchy and rejects overflow atomically", BlueprintInsertionIsAtomic);
            Run("Blueprint empty group is editable and undoable", BlueprintEmptyGroupIsUndoable);
            Run("Blueprint duplicate limit is atomic", BlueprintDuplicateLimitIsAtomic);
            Run("Blueprint scale is atomic and bounded", BlueprintScaleIsAtomicAndBounded);
            Run("Blueprint scale absorbs only boundary roundoff", BlueprintScaleAbsorbsOnlyBoundaryRoundoff);
            Run("Blueprint group reset preserves arrangement and is atomic", BlueprintGroupResetPreservesArrangementAndIsAtomic);
            Run("Blueprint history restores clean revisions", BlueprintHistoryRestoresCleanRevisions);
            Run("Blueprint group deletion reparents or removes children", BlueprintGroupDeletionReparentsOrRemovesChildren);
            Run("Blueprint primary part is optional and undoable", BlueprintPrimaryPartIsOptionalAndUndoable);
            Run("Blueprint primary group is optional and undoable", BlueprintPrimaryGroupIsOptionalAndUndoable);
            Run("Blueprint group pivots are scoped, remapped, and undoable", BlueprintGroupPivotsAreScopedRemappedAndUndoable);
            Run("Blueprint document rejects invalid graphs", BlueprintDocumentRejectsInvalidGraphs);
            Run("Blueprint failed edits roll back atomically", BlueprintFailedEditsRollBackAtomically);
            Run("Blueprint duplication is one atomic edit", BlueprintDuplicationIsOneAtomicEdit);
            Run("Blueprint array preview matches one atomic apply", BlueprintArrayPreviewMatchesApply);
            Run("Blueprint array dimensions include the source cell", BlueprintArrayCountsIncludeSource);
            Run("Blueprint array uses the resolved group pivot", BlueprintArrayUsesResolvedGroupPivot);
            Run("Shared repeat scale keeps scaled hinges and symmetric rows", RepeatScaleHinges);
            Run("Blueprint array enforces scale and part limits", BlueprintArrayEnforcesLimits);
            Run("Blueprint contour follows support poses and applies atomically", BlueprintContourPreviewMatchesApply);
            Run("Blueprint contour validates supports, scale, and part limits", BlueprintContourEnforcesLimits);
            Run("Blueprint rollback drops failed edit", BlueprintRollbackDropsFailedEdit);
            Run("Blueprint rollback preserves prior redo", BlueprintRollbackPreservesPriorRedo);
            Run("Blueprint adopts saved identity without dirtying", BlueprintAdoptsSavedIdentity);
            Run("Blueprint metadata edit is atomic", BlueprintMetadataEditIsAtomic);
            Run("Blueprint inspector edit is atomic", BlueprintInspectorEditIsAtomic);
            Run("Blueprint collection views cannot mutate document", BlueprintCollectionViewsCannotMutateDocument);
            Run("Blueprint limits and history are bounded", BlueprintLimitsAndHistoryAreBounded);
            Run("Blueprint layout stays inside safe area", BlueprintLayoutStaysInsideSafeArea);
            Run("Blueprint catalog uses whole twelve by four pages", BlueprintCatalogUsesWholePages);

            if (failures == 0)
            {
                Console.WriteLine("All BuildWorks geometry tests passed.");
                return 0;
            }

            Console.Error.WriteLine($"{failures} BuildWorks geometry test(s) failed.");
            return 1;
        }

        private static CubicBezierCurve Curve()
        {
            return new CubicBezierCurve(
                new Point2(0.0, 0.0),
                new Point2(0.0, 10.0),
                new Point2(10.0, 10.0),
                new Point2(10.0, 0.0));
        }

        private static void CurveEndpointsAreExact()
        {
            Near(new Point2(0.0, 0.0), Curve().Evaluate(0.0));
            Near(new Point2(10.0, 0.0), Curve().Evaluate(1.0));
        }

        private static void CurveMidpointIsExact()
        {
            Near(new Point2(5.0, 7.5), Curve().Evaluate(0.5));
        }

        private static void CurveSamplingIsDeterministic()
        {
            IReadOnlyList<Point2> first = Curve().Sample(8);
            IReadOnlyList<Point2> second = Curve().Sample(8);
            Equal(9, first.Count);
            for (int i = 0; i < first.Count; ++i)
            {
                Near(first[i], second[i]);
            }
            Throws<ArgumentOutOfRangeException>(() => Curve().Sample(0));
            Throws<ArgumentOutOfRangeException>(() => Curve().Sample(CubicBezierCurve.MaximumSampleSegments + 1));
            Throws<ArgumentOutOfRangeException>(() => Curve().Evaluate(-0.01));
            Throws<ArgumentOutOfRangeException>(() => Curve().Evaluate(double.NaN));
        }

        private static void InvalidCurvePointsAreRejected()
        {
            Throws<ArgumentOutOfRangeException>(() => new CubicBezierCurve(
                new Point2(double.NaN, 0.0),
                new Point2(0.0, 1.0),
                new Point2(1.0, 1.0),
                new Point2(1.0, 0.0)));
        }

        private static void DefaultCurveRemainsFinite()
        {
            Near(new Point2(0.0, 0.0), default(CubicBezierCurve).Evaluate(0.5));
        }

        private static void PrecisionMovementQuantizesSymmetrically()
        {
            Near(0.05, PrecisionAdjustment.Quantize(0.026, 0.05));
            Near(-0.05, PrecisionAdjustment.Quantize(-0.026, 0.05));
            Near(0.01, PrecisionAdjustment.Quantize(0.005, 0.01));
            Near(-0.01, PrecisionAdjustment.Quantize(-0.005, 0.01));
            Near(0.0, PrecisionAdjustment.Quantize(0.004, 0.01));
        }

        private static void PrecisionAnglesNormalizeConsistently()
        {
            Near(-180.0, PrecisionAdjustment.NormalizeDegrees(180.0));
            Near(-179.9, PrecisionAdjustment.NormalizeDegrees(180.1));
            Near(179.9, PrecisionAdjustment.NormalizeDegrees(-180.1));
            Near(0.1, PrecisionAdjustment.NormalizeDegrees(720.1), 1e-8);
        }

        private static void InvalidPrecisionValuesAreRejected()
        {
            Throws<ArgumentOutOfRangeException>(() => PrecisionAdjustment.Quantize(double.NaN, 0.01));
            Throws<ArgumentOutOfRangeException>(() => PrecisionAdjustment.Quantize(1.0, 0.0));
            Throws<ArgumentOutOfRangeException>(() => PrecisionAdjustment.Quantize(1.0, double.PositiveInfinity));
            Throws<ArgumentOutOfRangeException>(() => PrecisionAdjustment.NormalizeDegrees(double.NaN));
            Throws<ArgumentOutOfRangeException>(() => PrecisionAdjustment.ClampOffset(double.NaN));
        }

        private static void PrecisionOffsetLimitIsSymmetric()
        {
            Near(PrecisionAdjustment.MaximumOffset, PrecisionAdjustment.ClampOffset(50.0));
            Near(-PrecisionAdjustment.MaximumOffset, PrecisionAdjustment.ClampOffset(-50.0));
            Near(2.5, PrecisionAdjustment.ClampOffset(2.5));
        }

        private static void AnchorPointsHaveExactOpposites()
        {
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(-4.0, -2.0, -3.0),
                new Point3(2.0, 3.0, 4.0)
            });
            for (int anchor = 0; anchor < AnchorAdjustment.AnchorCount; ++anchor)
            {
                int opposite = AnchorAdjustment.OppositeAnchor(anchor);
                Equal(anchor, AnchorAdjustment.OppositeAnchor(opposite));
                Near(bounds.Center * 2.0, bounds.Anchor(anchor) + bounds.Anchor(opposite));
            }
        }

        private static void AnchorBoundsCombineMultipleMeshes()
        {
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(-1.0, -2.0, -3.0),
                new Point3(2.0, 1.0, 4.0),
                new Point3(-4.0, 3.0, 1.0)
            });
            Near(new Point3(-4.0, -2.0, -3.0), bounds.Corner(0));
            Near(new Point3(2.0, 3.0, 4.0), bounds.Corner(7));
        }

        private static void AnchorEdgesUseExactMidpoints()
        {
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(-4.0, -2.0, -3.0),
                new Point3(2.0, 3.0, 4.0)
            });
            Near(new Point3(-1.0, -2.0, -3.0), bounds.Anchor(8));
            Near(new Point3(-1.0, 3.0, 4.0), bounds.Anchor(11));
            Near(new Point3(-4.0, 0.5, -3.0), bounds.Anchor(12));
            Near(new Point3(2.0, 0.5, 4.0), bounds.Anchor(15));
            Near(new Point3(-4.0, -2.0, 0.5), bounds.Anchor(16));
            Near(new Point3(2.0, 3.0, 0.5), bounds.Anchor(19));
        }

        private static void CenterAnchorMatchesBoundsCenter()
        {
            Equal(20, AnchorAdjustment.CenterAnchorIndex);
            Equal(21, AnchorAdjustment.SelectableAnchorCount);
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(-4.0, -2.0, -3.0),
                new Point3(2.0, 3.0, 4.0)
            });
            Near(new Point3(-1.0, 0.5, 0.5), bounds.Center);
        }

        private static void AnchoredRotationPreservesFixedCorner()
        {
            Point3 fixedWorld = new Point3(10.0, 2.0, -3.0);
            Point3 fixedLocal = new Point3(-1.0, 0.5, 2.0);
            Rotation3 rotation = new Rotation3(0.2, -0.3, 0.4, 0.8);
            Point3 position = AnchorAdjustment.PositionForFixedAnchor(
                fixedWorld,
                fixedLocal,
                rotation);
            Point3 reconstructed = position + rotation.Rotate(fixedLocal);
            Near(fixedWorld, reconstructed);
        }

        private static void CenteredRotationPreservesBoundsCenter()
        {
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(-4.0, -2.0, -3.0),
                new Point3(2.0, 3.0, 4.0)
            });
            Point3 centerWorld = new Point3(10.0, 2.0, -3.0);
            Rotation3 rotation = new Rotation3(0.2, -0.3, 0.4, 0.8);
            Point3 position = AnchorAdjustment.PositionForFixedAnchor(
                centerWorld,
                bounds.Center,
                rotation);
            Near(centerWorld, position + rotation.Rotate(bounds.Center));
        }

        private static void MagneticMovementLandsSourceAnchor()
        {
            Point3 targetWorld = new Point3(7.0, -1.0, 12.0);
            Point3 sourceLocal = new Point3(-2.0, 0.5, 3.0);
            Rotation3 rotation = new Rotation3(-0.1, 0.4, 0.2, 0.85);
            Point3 position = AnchorAdjustment.PositionForFixedAnchor(
                targetWorld,
                sourceLocal,
                rotation);
            Near(targetWorld, position + rotation.Rotate(sourceLocal));
        }

        private static void AnchorFocusCompensationIsExact()
        {
            Point3 oldPosition = new Point3(2.0, 3.0, 4.0);
            Point3 newPosition = new Point3(-1.0, 5.0, 8.0);
            Point3 oldOffset = new Point3(0.5, -2.0, 1.0);
            Point3 newOffset = AnchorAdjustment.CompensateFocusOffset(
                oldOffset,
                oldPosition,
                newPosition);
            Near(oldPosition + oldOffset, newPosition + newOffset);
        }

        private static void ConstrainedAnchorRotationUsesSelectedAxis()
        {
            Near(90.0, AnchorAdjustment.ConstrainedAngleDegrees(
                new Point3(2.0, 1.0, 0.0),
                new Point3(-4.0, 0.0, 1.0),
                new Point3(1.0, 0.0, 0.0)));
            Near(-90.0, AnchorAdjustment.ConstrainedAngleDegrees(
                new Point3(0.0, 1.0, 0.0),
                new Point3(0.0, 0.0, -1.0),
                new Point3(1.0, 0.0, 0.0)));
        }

        private static void UndoHistoryKeepsNewestTwentyActions()
        {
            BoundedUndoHistory<int> history = new BoundedUndoHistory<int>(20);
            history.Reset(0);
            for (int value = 1; value <= 25; ++value)
            {
                history.Commit(value);
            }
            Equal(20, history.UndoCount);
            for (int expected = 24; expected >= 5; --expected)
            {
                if (!history.TryUndo(out int actual))
                {
                    throw new InvalidOperationException("Expected an undo state.");
                }
                Equal(expected, actual);
            }
            Equal(0, history.UndoCount);
            Equal(false, history.TryUndo(out _));
        }

        private static void UndoHistoryRestoresRedoneActions()
        {
            BoundedUndoHistory<int> history = new BoundedUndoHistory<int>(20);
            history.Reset(0);
            history.Commit(1);
            history.Commit(2);
            Equal(true, history.TryUndo(out int undone));
            Equal(1, undone);
            Equal(1, history.RedoCount);
            Equal(true, history.TryRedo(out int redone));
            Equal(2, redone);
            Equal(0, history.RedoCount);
        }

        private static void NewEditDiscardsRedoBranch()
        {
            BoundedUndoHistory<int> history = new BoundedUndoHistory<int>(20);
            history.Reset(0);
            history.Commit(1);
            history.Commit(2);
            history.TryUndo(out _);
            history.Commit(3);
            Equal(false, history.TryRedo(out _));
            Equal(true, history.TryUndo(out int state));
            Equal(1, state);
        }

        private static void SnapNeighborsExcludeRectangleDiagonals()
        {
            Point3[] points =
            {
                new Point3(-2.0, -1.0, 0.0),
                new Point3(2.0, -1.0, 0.0),
                new Point3(2.0, 1.0, 0.0),
                new Point3(-2.0, 1.0, 0.0)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(points);
            Equal(4, edges.Count);
            Equal(false, HasEdge(edges, points[0], points[2]));
            Equal(false, HasEdge(edges, points[1], points[3]));
        }

        private static void SnapNeighborsSplitExistingLine()
        {
            Point3[] points =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0),
                new Point3(4.0, 0.0, 0.0)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(points);
            Equal(2, edges.Count);
            Equal(true, HasEdge(edges, points[0], points[1]));
            Equal(true, HasEdge(edges, points[1], points[2]));
            Equal(false, HasEdge(edges, points[0], points[2]));
        }

        private static void FeatureEdgesExcludeFlatTriangulation()
        {
            Point3[] vertices =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(1.0, 1.0, 0.0),
                new Point3(0.0, 1.0, 0.0)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ExtractFeatureEdges(
                vertices,
                new[] { 0, 1, 2, 0, 2, 3 });
            Equal(4, edges.Count);
            Equal(false, HasEdge(edges, vertices[0], vertices[2]));
        }

        private static void FeatureEdgesKeepVisibleCreases()
        {
            Point3[] vertices =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(0.0, 1.0, 0.0),
                new Point3(0.0, 0.0, 1.0)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ExtractFeatureEdges(
                vertices,
                new[] { 0, 1, 2, 0, 3, 1 });
            Equal(5, edges.Count);
            Equal(true, HasEdge(edges, vertices[0], vertices[1]));
        }

        private static void FeatureEdgesWeldDuplicatedSeams()
        {
            Point3[] vertices =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(1.0, 1.0, 0.0),
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 1.0, 0.0),
                new Point3(0.0, 1.0, 0.0)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ExtractFeatureEdges(
                vertices,
                new[] { 0, 1, 2, 3, 4, 5 });
            Equal(4, edges.Count);
            Equal(false, HasEdge(edges, vertices[0], vertices[2]));
        }

        private static bool HasEdge(
            IReadOnlyList<Edge3> edges,
            Point3 first,
            Point3 second)
        {
            foreach (Edge3 edge in edges)
            {
                if ((SamePoint(edge.Start, first) && SamePoint(edge.End, second)) ||
                    (SamePoint(edge.Start, second) && SamePoint(edge.End, first)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SamePoint(Point3 first, Point3 second) =>
            (first - second).LengthSquared < 0.0000000001;

        private static void NonManifoldEdgesRemainSnapTargets()
        {
            Point3[] vertices =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(0.0, 1.0, 0.0),
                new Point3(0.0, 0.9, 0.1),
                new Point3(0.0, 0.9, -0.1)
            };
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ExtractFeatureEdges(
                vertices,
                new[] { 0, 1, 2, 0, 1, 3, 0, 1, 4 });
            Equal(true, HasEdge(edges, vertices[0], vertices[1]));
        }

        private static void ScreenEdgeSelectionIsPerspectiveCorrect()
        {
            Near(0.5, AnchorAdjustment.PerspectiveSegmentParameter(
                10.0 / 11.0,
                1.0,
                10.0));
            Near(0.0, AnchorAdjustment.PerspectiveSegmentParameter(0.0, 1.0, 10.0));
            Near(1.0, AnchorAdjustment.PerspectiveSegmentParameter(1.0, 1.0, 10.0));
        }

        private static void InvalidAnchorValuesAreRejected()
        {
            Throws<ArgumentOutOfRangeException>(() => AnchorAdjustment.OppositeAnchor(-1));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.OppositeAnchor(AnchorAdjustment.AnchorCount));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.CreateBounds(Array.Empty<Point3>()));
            Throws<ArgumentOutOfRangeException>(() => AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(double.NaN, 0.0, 0.0)
            }));
            AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 1.0, 1.0)
            });
            Throws<ArgumentOutOfRangeException>(() =>
                bounds.Anchor(AnchorAdjustment.AnchorCount));
            Throws<ArgumentOutOfRangeException>(() => AnchorAdjustment.PositionForFixedAnchor(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 1.0, 1.0),
                new Rotation3(0.0, 0.0, 0.0, 0.0)));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.ConstrainedAngleDegrees(
                    new Point3(1.0, 0.0, 0.0),
                    new Point3(1.0, 0.0, 0.0),
                    new Point3(1.0, 0.0, 0.0)));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.ConstrainedAngleDegrees(
                    new Point3(double.NaN, 0.0, 0.0),
                    new Point3(0.0, 1.0, 0.0),
                    new Point3(1.0, 0.0, 0.0)));
            Throws<ArgumentOutOfRangeException>(() => new BoundedUndoHistory<int>(0));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.ExtractFeatureEdges(
                    new[] { new Point3(0.0, 0.0, 0.0) },
                    new[] { 0, 1, 0 }));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.ExtractFeatureEdges(
                    Array.Empty<Point3>(),
                    Array.Empty<int>(),
                    -1.0));
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.PerspectiveSegmentParameter(0.5, -1.0, 1.0));
        }

        private static void LineToLineSamplesMatchingFractions()
        {
            IReadOnlyList<GuidePair3> samples = ConstructionLayout.SampleStraightGuides(
                new Point3(0.0, 0.0, 0.0),
                new Point3(10.0, 0.0, 0.0),
                false,
                new Point3(0.0, 5.0, 0.0),
                new Point3(20.0, 5.0, 0.0),
                false,
                3);
            Near(new Point3(5.0, 0.0, 0.0), samples[1].Contact);
            Near(new Point3(10.0, 5.0, 0.0), samples[1].Aim);
        }

        private static void StraightRepeatUsesExactStep()
        {
            IReadOnlyList<Point3> samples = ConstructionLayout.SampleStraightRepeat(
                new Point3(1.0, 2.0, 3.0),
                new Point3(-0.5, 0.0, 2.0),
                4);
            Near(new Point3(1.0, 2.0, 3.0), samples[0]);
            Near(new Point3(-0.5, 2.0, 9.0), samples[3]);
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.SampleStraightRepeat(
                    new Point3(0.0, 0.0, 0.0),
                    new Point3(0.0, 0.0, 0.0),
                    2));
        }

        private static void RotationDragFollowsBothCameraSides()
        {
            Near(30.0, PrecisionAdjustment.ScreenAlignedRotationDelta(30.0, 1.0));
            Near(-30.0, PrecisionAdjustment.ScreenAlignedRotationDelta(30.0, -1.0));
            Throws<ArgumentOutOfRangeException>(() =>
                PrecisionAdjustment.ScreenAlignedRotationDelta(double.NaN, 1.0));
        }

        private static void IncrementalTurnFollowsRowDirection()
        {
            Near(15.0, PrecisionAdjustment.SignedForDirection(15.0, 2.0));
            Near(-15.0, PrecisionAdjustment.SignedForDirection(15.0, -2.0));
            Near(15.0, PrecisionAdjustment.SignedForDirection(-15.0, -2.0));
            Throws<ArgumentOutOfRangeException>(() =>
                PrecisionAdjustment.SignedForDirection(15.0, double.NaN));
        }

        private static void ConnectedRingStartsAtSelectedSupport()
        {
            Point3[][] supports = ClosedSegments(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 1.0),
                new Point3(0.0, 0.0, 1.0));
            Array.Resize(ref supports, 5);
            supports[4] = new[]
            {
                new Point3(10.0, 0.0, 0.0),
                new Point3(11.0, 0.0, 0.0)
            };
            IReadOnlyList<int> ring = ConstructionLayout.OrderConnectedContour(
                supports,
                1,
                out bool closed);
            Equal(true, closed);
            Equal(4, ring.Count);
            Equal(1, ring[0]);
            HashSet<int> members = new HashSet<int>(ring);
            Equal(4, members.Count);
            for (int index = 0; index < 4; ++index) Equal(true, members.Contains(index));
        }

        private static void ConnectedContourRejectsInvalidInput()
        {
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.OrderConnectedContour(null, 0, out _));
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.OrderConnectedContour(
                    new[] { Array.Empty<Point3>() }, 0, out _));
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.OrderConnectedContour(
                    new[] { new[] { new Point3(double.NaN, 0.0, 0.0) } }, 0, out _));
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.OrderConnectedContour(
                    new[] { new[] { new Point3(0.0, 0.0, 0.0) } }, 1, out _));
        }

        private static void ConcaveRingFollowsGraphNeighbors()
        {
            Point3[] anchors =
            {
                new Point3(0, 0, 0), new Point3(1, 0, 0), new Point3(2, 0, 0),
                new Point3(3, 0, 0), new Point3(4, 0, 0), new Point3(5, 0, 0),
                new Point3(6, 0, 0), new Point3(6, 0, 1), new Point3(6, 0, 2),
                new Point3(5, 0, 2), new Point3(4, 0, 2), new Point3(3, 0, 2),
                new Point3(2, 0, 2), new Point3(2, 0, 3), new Point3(2, 0, 4),
                new Point3(3, 0, 4), new Point3(4, 0, 4), new Point3(5, 0, 4),
                new Point3(6, 0, 4), new Point3(6, 0, 5), new Point3(6, 0, 6),
                new Point3(5, 0, 6), new Point3(4, 0, 6), new Point3(3, 0, 6),
                new Point3(2, 0, 6), new Point3(1, 0, 6), new Point3(0, 0, 6),
                new Point3(0, 0, 5), new Point3(0, 0, 4), new Point3(0, 0, 3),
                new Point3(0, 0, 2), new Point3(0, 0, 1)
            };
            IReadOnlyList<int> ring = ConstructionLayout.OrderConnectedContour(
                ClosedSegments(anchors),
                0,
                out bool closed);
            Equal(true, closed);
            Equal(anchors.Length, ring.Count);
            Equal(anchors.Length, new HashSet<int>(ring).Count);
        }

        private static void ConnectedContourAcceptsOpenChains()
        {
            Point3[][] open = OpenSegments(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0),
                new Point3(3.0, 0.0, 0.0));
            IReadOnlyList<int> chain = ConstructionLayout.OrderConnectedContour(
                open,
                1,
                out bool chainClosed);
            Equal(false, chainClosed);
            Equal(3, chain.Count);
            Equal(0, chain[0]);
            Equal(1, chain[1]);
            Equal(2, chain[2]);
        }

        private static void ConnectedContourAcceptsSeventyTwoPieceRing()
        {
            Point3[] points = new Point3[72];
            for (int index = 0; index < points.Length; ++index)
            {
                double angle = index * Math.PI * 2.0 / points.Length;
                points[index] = new Point3(Math.Cos(angle) * 10.0, 0.0,
                    Math.Sin(angle) * 10.0);
            }
            IReadOnlyList<int> ring = ConstructionLayout.OrderConnectedContour(
                ClosedSegments(points),
                17,
                out bool closed);
            Equal(true, closed);
            Equal(72, ring.Count);
            Equal(17, ring[0]);
        }

        private static void TouchingContourAcceptsOverlappingRingEdges()
        {
            Point3[] vertices =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 2.0),
                new Point3(0.0, 0.0, 2.0)
            };
            Point3[][] supports = ExtendedClosedSegments(vertices, 0.35);
            Equal(1, ConstructionLayout.OrderConnectedContour(
                supports,
                2,
                out _).Count);
            IReadOnlyList<int> ring = ConstructionLayout.OrderTouchingContour(
                supports,
                2,
                out bool closed);
            Equal(true, closed);
            Equal(4, ring.Count);
            Equal(2, ring[0]);
            Equal(4, new HashSet<int>(ring).Count);
        }

        private static void TouchingContourKeepsNearbyRowsSeparate()
        {
            Point3[][] supports =
            {
                new[] { new Point3(-0.3, 0.0, 0.0), new Point3(1.3, 0.0, 0.0) },
                new[] { new Point3(0.7, 0.0, 0.0), new Point3(2.3, 0.0, 0.0) },
                new[] { new Point3(1.7, 0.0, 0.0), new Point3(3.3, 0.0, 0.0) },
                new[] { new Point3(-0.3, 0.0, 0.1), new Point3(1.3, 0.0, 0.1) },
                new[] { new Point3(0.7, 0.0, 0.1), new Point3(2.3, 0.0, 0.1) },
                new[] { new Point3(1.7, 0.0, 0.1), new Point3(3.3, 0.0, 0.1) }
            };
            IReadOnlyList<int> chain = ConstructionLayout.OrderTouchingContour(
                supports,
                1,
                out bool closed);
            Equal(false, closed);
            Equal(3, chain.Count);
            for (int index = 0; index < chain.Count; ++index)
                Equal(true, chain[index] < 3);
        }

        private static void ConnectedContourIgnoresRingBranches()
        {
            Point3[][] supports = ClosedSegments(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 1.0),
                new Point3(0.0, 0.0, 1.0));
            Array.Resize(ref supports, 6);
            supports[4] = new[]
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(-1.0, 0.0, 0.0)
            };
            supports[5] = new[]
            {
                new Point3(-1.0, 0.0, 0.0),
                new Point3(-2.0, 0.0, 0.0)
            };
            IReadOnlyList<int> ring = ConstructionLayout.OrderConnectedContour(
                supports,
                0,
                out bool ringClosed);
            Equal(true, ringClosed);
            Equal(4, ring.Count);
            for (int index = 0; index < ring.Count; ++index)
            {
                if (ring[index] == 4)
                    throw new InvalidOperationException("Branch leaked into the primary ring.");
            }
        }

        private static void ConnectedContourRejectsForkedOpenChains()
        {
            Point3[][] fork =
            {
                new[] { new Point3(0.0, 0.0, 0.0), new Point3(1.0, 0.0, 0.0) },
                new[] { new Point3(1.0, 0.0, 0.0), new Point3(2.0, 0.0, 0.0) },
                new[] { new Point3(1.0, 0.0, 0.0), new Point3(1.0, 0.0, 1.0) }
            };
            Equal(0, ConstructionLayout.OrderConnectedContour(
                fork,
                0,
                out _).Count);
        }

        private static void ConnectedContourDoesNotMergeNearbyRows()
        {
            Point3[][] rows =
            {
                new[] { new Point3(0.0, 0.0, 0.0), new Point3(1.0, 0.0, 0.0) },
                new[] { new Point3(1.0, 0.0, 0.0), new Point3(2.0, 0.0, 0.0) },
                new[] { new Point3(0.0, 0.0, 0.25), new Point3(1.0, 0.0, 0.25) },
                new[] { new Point3(1.0, 0.0, 0.25), new Point3(2.0, 0.0, 0.25) }
            };
            IReadOnlyList<int> chain = ConstructionLayout.OrderConnectedContour(
                rows,
                0,
                out bool closed);
            Equal(false, closed);
            Equal(2, chain.Count);
            Equal(0, chain[0]);
            Equal(1, chain[1]);
        }

        private static Point3[][] OpenSegments(params Point3[] vertices)
        {
            Point3[][] supports = new Point3[vertices.Length - 1][];
            for (int index = 0; index < supports.Length; ++index)
                supports[index] = new[] { vertices[index], vertices[index + 1] };
            return supports;
        }

        private static Point3[][] ClosedSegments(params Point3[] vertices)
        {
            Point3[][] supports = new Point3[vertices.Length][];
            for (int index = 0; index < supports.Length; ++index)
                supports[index] = new[]
                {
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]
                };
            return supports;
        }

        private static Point3[][] ExtendedClosedSegments(
            Point3[] vertices,
            double extension)
        {
            Point3[][] supports = new Point3[vertices.Length][];
            for (int index = 0; index < vertices.Length; ++index)
            {
                Point3 start = vertices[index];
                Point3 end = vertices[(index + 1) % vertices.Length];
                Point3 direction = end - start;
                double length = Math.Sqrt(direction.LengthSquared);
                direction *= extension / length;
                supports[index] = new[] { start - direction, end + direction };
            }
            return supports;
        }

        private static void LineToPointKeepsOneAim()
        {
            IReadOnlyList<GuidePair3> samples = ConstructionLayout.SampleStraightGuides(
                new Point3(0.0, 0.0, 0.0),
                new Point3(4.0, 0.0, 0.0),
                false,
                new Point3(2.0, 3.0, 0.0),
                new Point3(2.0, 3.0, 0.0),
                true,
                5);
            foreach (GuidePair3 sample in samples)
            {
                Near(new Point3(2.0, 3.0, 0.0), sample.Aim);
            }
        }

        private static void PointToLineKeepsOneContact()
        {
            IReadOnlyList<GuidePair3> samples = ConstructionLayout.SampleStraightGuides(
                new Point3(1.0, 2.0, 3.0),
                new Point3(1.0, 2.0, 3.0),
                true,
                new Point3(-2.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0),
                false,
                3);
            foreach (GuidePair3 sample in samples)
            {
                Near(new Point3(1.0, 2.0, 3.0), sample.Contact);
            }
            Near(new Point3(0.0, 0.0, 0.0), samples[1].Aim);
        }

        private static void InvalidStraightGuidesAreRejected()
        {
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.SampleStraightGuides(
                    new Point3(0.0, 0.0, 0.0),
                    new Point3(0.0, 0.0, 0.0),
                    false,
                    new Point3(0.0, 1.0, 0.0),
                    new Point3(1.0, 1.0, 0.0),
                    false,
                    2));
            Throws<ArgumentOutOfRangeException>(() =>
                ConstructionLayout.SampleStraightGuides(
                    new Point3(0.0, 0.0, 0.0),
                    new Point3(1.0, 0.0, 0.0),
                    false,
                    new Point3(0.0, 1.0, 0.0),
                    new Point3(1.0, 1.0, 0.0),
                    false,
                    ConstructionLayout.MaximumCopies + 1));
        }

        private static void PolylineSamplesByTraveledDistance()
        {
            IReadOnlyList<Point3> samples = GuidePathSampling.SamplePolyline(
                new[]
                {
                    new Point3(0.0, 0.0, 0.0),
                    new Point3(1.0, 0.0, 0.0),
                    new Point3(1.0, 3.0, 0.0)
                },
                5);
            Near(new Point3(1.0, 0.0, 0.0), samples[1]);
            Near(new Point3(1.0, 1.0, 0.0), samples[2]);
            Near(new Point3(1.0, 2.0, 0.0), samples[3]);
        }

        private static void ArcPassesThroughControlPoint()
        {
            IReadOnlyList<Point3> samples = GuidePathSampling.SampleArc(
                new Point3(1.0, 0.0, 0.0),
                new Point3(0.0, -1.0, 0.0),
                new Point3(0.0, 1.0, 0.0),
                4);
            Near(new Point3(1.0, 0.0, 0.0), samples[0]);
            Near(new Point3(0.0, -1.0, 0.0), samples[1]);
            Near(new Point3(0.0, 1.0, 0.0), samples[3]);
        }

        private static void BezierSamples3DCurveByTraveledDistance()
        {
            IReadOnlyList<Point3> samples = GuidePathSampling.SampleBezier(
                new Point3(0.0, 0.0, 0.0),
                new Point3(0.0, 0.0, 0.0),
                new Point3(0.0, 0.0, 0.0),
                new Point3(8.0, 0.0, 0.0),
                5);
            Near(new Point3(0.0, 0.0, 0.0), samples[0]);
            Near(new Point3(2.0, 0.0, 0.0), samples[1], 0.01);
            Near(new Point3(4.0, 0.0, 0.0), samples[2], 0.01);
            Near(new Point3(6.0, 0.0, 0.0), samples[3], 0.01);
            Near(new Point3(8.0, 0.0, 0.0), samples[4]);

            IReadOnlyList<Point3> repeated = GuidePathSampling.SampleBezier(
                new Point3(0.0, 0.0, 0.0),
                new Point3(0.0, 0.0, 0.0),
                new Point3(0.0, 0.0, 0.0),
                new Point3(8.0, 0.0, 0.0),
                5);
            for (int index = 0; index < samples.Count; ++index)
                Near(samples[index], repeated[index]);
        }

        private static void RepeatModifiersAndSymmetryAreDeterministic()
        {
            IReadOnlyList<LayoutTransform3> samples = GuidePathSampling.SampleRepeat(
                new Point3(0.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0),
                5,
                0.25,
                new Point3(0.0, 1.0, 0.0),
                10.0,
                true,
                new Point3(-1.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0));
            Near(new Point3(0.0, 0.0, 0.0), samples[0].Position);
            Near(new Point3(1.98480775, 0.25, -0.17364818), samples[1].Position);
            Near(new Point3(-1.98480775, -0.25, -0.17364818), samples[2].Position);
            Near(20.0, samples[3].IncrementalRotationDegrees);
            Near(-20.0, samples[4].IncrementalRotationDegrees);
            Near(new Point3(0.0, 1.0, 0.0), samples[4].RotationAxis);

            IReadOnlyList<LayoutTransform3> straight = GuidePathSampling.SampleRepeat(
                new Point3(1.0, 2.0, 3.0),
                new Point3(2.0, 0.0, -1.0),
                4,
                0.25,
                new Point3(0.0, 2.0, 0.0),
                0.0,
                false,
                new Point3(-1.0, 0.0, 0.5),
                new Point3(1.0, 0.0, -0.5));
            Near(new Point3(7.0, 2.75, 0.0), straight[3].Position);
            Near(0.0, straight[3].IncrementalRotationDegrees);
            Near(new Point3(0.0, 1.0, 0.0), straight[3].RotationAxis);

            IReadOnlyList<LayoutTransform3> curved = GuidePathSampling.SampleRepeat(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                3,
                0.0,
                new Point3(0.0, 1.0, 0.0),
                90.0,
                false,
                new Point3(-0.5, 0.0, 0.0),
                new Point3(0.5, 0.0, 0.0));
            Near(new Point3(0.5, 0.0, -0.5), curved[1].Position);
            Near(new Point3(0.0, 0.0, -1.0), curved[2].Position);
            Near(180.0, curved[2].IncrementalRotationDegrees);
        }

        private static void RepeatBendsIntoVerticalArch()
        {
            IReadOnlyList<LayoutTransform3> samples = GuidePathSampling.SampleRepeat(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                3,
                0.0,
                new Point3(0.0, 0.0, 1.0),
                90.0,
                false,
                new Point3(-0.5, 0.0, 0.0),
                new Point3(0.5, 0.0, 0.0));

            Near(new Point3(0.5, 0.5, 0.0), samples[1].Position);
            Near(new Point3(0.0, 1.0, 0.0), samples[2].Position);
            Near(new Point3(0.0, 0.0, 1.0), samples[2].RotationAxis);
            Near(180.0, samples[2].IncrementalRotationDegrees);
        }

        private static void RepeatRollsWithoutChangingStraightPath()
        {
            IReadOnlyList<LayoutTransform3> samples = GuidePathSampling.SampleRepeat(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                3,
                0.0,
                new Point3(1.0, 0.0, 0.0),
                90.0,
                false,
                new Point3(-0.5, 0.0, 0.0),
                new Point3(0.5, 0.0, 0.0));

            Near(new Point3(1.0, 0.0, 0.0), samples[1].Position);
            Near(new Point3(2.0, 0.0, 0.0), samples[2].Position);
            Near(new Point3(1.0, 0.0, 0.0), samples[2].RotationAxis);
            Near(180.0, samples[2].IncrementalRotationDegrees);
        }

        private static void RepeatAcceptsArbitraryFractionalAngles()
        {
            IReadOnlyList<LayoutTransform3> samples = GuidePathSampling.SampleRepeat(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                3,
                0.0,
                new Point3(0.0, 1.0, 0.0),
                12.35,
                false,
                new Point3(-0.5, 0.0, 0.0),
                new Point3(0.5, 0.0, 0.0));

            Near(12.35, samples[1].IncrementalRotationDegrees);
            Near(24.7, samples[2].IncrementalRotationDegrees);
        }

        private static void RepeatRejectsUnsafeModifiersAndLimits()
        {
            Point3 origin = new Point3(0.0, 0.0, 0.0);
            Point3 step = new Point3(1.0, 0.0, 0.0);
            Point3 axis = new Point3(0.0, 1.0, 0.0);
            Point3 back = new Point3(-0.5, 0.0, 0.0);
            Point3 front = new Point3(0.5, 0.0, 0.0);
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step, 1, 0.0, axis, 0.0, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step,
                    ConstructionLayout.MaximumCopies + 1,
                    0.0, axis, 0.0, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, origin, 2, 0.0, axis, 0.0, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step, 2, double.NaN, axis, 0.0, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step, 2, 0.0, axis, double.NaN, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step, 2, 0.0, origin, 0.0, false, back, front));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SampleRepeat(origin, step, 2, 0.0, axis, 0.0, false, origin, origin));
        }

        private static void PlaneSamplesBothAxes()
        {
            IReadOnlyList<Point3> samples = GuidePathSampling.SamplePlane(
                new Point3(1.0, 2.0, 3.0),
                new Point3(2.0, 0.0, 0.0),
                3,
                new Point3(0.0, 0.0, 4.0),
                2,
                false);
            Equal(6, samples.Count);
            Near(new Point3(1.0, 2.0, 3.0), samples[0]);
            Near(new Point3(3.0, 2.0, 3.0), samples[1]);
            Near(new Point3(5.0, 2.0, 3.0), samples[2]);
            Near(new Point3(1.0, 2.0, 7.0), samples[3]);
            Near(new Point3(5.0, 2.0, 7.0), samples[5]);

            IReadOnlyList<Point3> symmetric = GuidePathSampling.SamplePlane(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                3,
                new Point3(0.0, 0.0, 2.0),
                2,
                true);
            Near(new Point3(0.0, 0.0, 0.0), symmetric[0]);
            Near(new Point3(1.0, 0.0, 0.0), symmetric[1]);
            Near(new Point3(-1.0, 0.0, 0.0), symmetric[2]);
            Near(new Point3(0.0, 0.0, 2.0), symmetric[3]);

            Equal(128, GuidePathSampling.SamplePlane(
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                16,
                new Point3(0.0, 0.0, 1.0),
                8,
                false).Count);
        }

        private static void PlaneRejectsUnsafeAxesAndCopyCounts()
        {
            Point3 origin = new Point3(0.0, 0.0, 0.0);
            Point3 x = new Point3(1.0, 0.0, 0.0);
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SamplePlane(origin, x, 3, x * 2.0, 2, false));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SamplePlane(origin, x, 17, new Point3(0.0, 0.0, 1.0), 8, false));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SamplePlane(origin, x, 1, new Point3(0.0, 0.0, 1.0), 2, false));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.SamplePlane(origin, x, 2, new Point3(double.NaN, 0.0, 1.0), 2, false));
        }

        private static void GuideSamplesPairPointsAndPaths()
        {
            IReadOnlyList<Point3> contact = GuidePathSampling.SampleLine(
                new Point3(0.0, 0.0, 0.0),
                new Point3(4.0, 0.0, 0.0),
                3);
            IReadOnlyList<GuidePair3> pairs = GuidePathSampling.PairSamples(
                contact,
                new[] { new Point3(2.0, 3.0, 0.0) },
                3);
            Near(new Point3(2.0, 0.0, 0.0), pairs[1].Contact);
            Near(new Point3(2.0, 3.0, 0.0), pairs[1].Aim);

            IReadOnlyList<GuidePair3> reverse = GuidePathSampling.PairSamples(
                new[] { new Point3(2.0, 3.0, 0.0) },
                contact,
                3);
            Near(new Point3(2.0, 3.0, 0.0), reverse[2].Contact);
            Near(new Point3(4.0, 0.0, 0.0), reverse[2].Aim);
        }

        private static void GuidePairingRejectsMismatchesAndCollisions()
        {
            Point3[] path =
            {
                new Point3(0.0, 0.0, 0.0),
                new Point3(1.0, 0.0, 0.0),
                new Point3(2.0, 0.0, 0.0)
            };
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.PairSamples(path, new[] { path[0], path[1] }, 3));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.PairSamples(path, path, 3));
            Throws<ArgumentOutOfRangeException>(() =>
                GuidePathSampling.PairSamples(
                    path,
                    new[] { new Point3(double.NaN, 0.0, 0.0) },
                    3));
        }

        private static void CompositeSnapPointsExcludeInternalJoins()
        {
            IReadOnlyList<Point3> external = AnchorAdjustment.ExternalCompositeSnapPoints(
                new IReadOnlyList<Point3>[]
                {
                    new[] { new Point3(0.0, 0.0, 0.0), new Point3(2.0, 0.0, 0.0) },
                    new[] { new Point3(2.04, 0.0, 0.0), new Point3(4.0, 0.0, 0.0) }
                });
            Equal(2, external.Count);
            Near(new Point3(0.0, 0.0, 0.0), external[0]);
            Near(new Point3(4.0, 0.0, 0.0), external[1]);
        }

        private static void CompositeSnapPointsKeepSamePartNeighbors()
        {
            IReadOnlyList<Point3> external = AnchorAdjustment.ExternalCompositeSnapPoints(
                new IReadOnlyList<Point3>[]
                {
                    new[] { new Point3(0.0, 0.0, 0.0), new Point3(0.04, 0.0, 0.0) }
                });
            Equal(1, external.Count);
            Throws<ArgumentOutOfRangeException>(() =>
                AnchorAdjustment.ExternalCompositeSnapPoints(
                    new IReadOnlyList<Point3>[] { new[] { new Point3(0.0, 0.0, 0.0) } },
                    0.0));
        }

        private static void BlueprintDocumentDeepCopiesSource()
        {
            BlueprintEditorPart source = EditorPart("part-a", 1.0, 0.0, 0.0);
            var document = EditorDocument(source);
            document.SelectOnly(source.StableId);

            Equal(true, document.ApplyTransformDelta(
                new Point3(2.0, 0.0, 0.0),
                IdentityRotation(),
                new Point3(0.0, 0.0, 0.0)));

            Near(new Point3(1.0, 0.0, 0.0), source.Position);
            Near(new Point3(3.0, 0.0, 0.0), document.Parts[0].Position);
        }

        private static void BlueprintSelectionDoesNotDirtyDocument()
        {
            var document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0),
                EditorPart("part-b", 1.0, 0.0, 0.0));

            Equal(true, document.SelectOnly("part-a"));
            Equal(true, document.ToggleSelection("part-b"));
            Equal(2, document.Selection.Count);
            Equal(false, document.IsDirty);
            document.ClearSelection();
            Equal(false, document.IsDirty);
            Equal(false, document.SelectOnly("missing"));
        }

        private static void BlueprintRangeSelectionFollowsTreeOrder()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();
            document.SelectOnly("part-a");

            Equal(true, document.SelectRange("part-b"));
            Equal(2, document.Selection.Count);
            Equal("part-a", document.Selection[0]);
            Equal("part-b", document.Selection[1]);
            Equal("part-b", document.ActiveNodeId);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintRangeSelectionStaysInsideParent()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();
            document.SelectOnly("group-a");

            Equal(true, document.SelectRange("part-b"));
            Equal(1, document.Selection.Count);
            Equal("part-b", document.Selection[0]);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintOutlinerCommandsAreAtomic()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();

            Equal(true, document.SetSelectionVisibility(false));
            Equal(false, document.Groups[0].Visible);
            Equal(true, document.Undo());
            Equal(true, document.Groups[0].Visible);
            Equal(false, document.IsDirty);

            Equal(true, document.SetSelectionLocked(true));
            Equal(true, document.Groups[0].Locked);
            Equal(true, document.Undo());

            Equal(true, document.UngroupSelection());
            Equal(0, document.Groups.Count);
            Equal<string>(null, document.Parts[0].ParentGroupId);
            Equal<string>(null, document.Parts[1].ParentGroupId);
            Equal(true, document.Undo());
            Equal(1, document.Groups.Count);

            document.SetVisibility("part-a", false);
            Equal(true, document.ShowAll());
            Equal(true, document.Parts[0].Visible);
        }

        private static void BlueprintVisibilityIsolationIsAtomic()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            Equal(true, document.AddPart(EditorPart("outside", 4.0, 0.0, 0.0)));
            document.MarkClean();

            document.SelectOnly("part-a");
            Equal(true, document.IsolateSelection());
            Equal(true, document.Parts[0].Visible);
            Equal(false, document.Parts[1].Visible);
            Equal(false, document.Parts[2].Visible);
            Equal(true, document.Groups[0].Visible);
            Equal(true, document.Undo());

            document.SelectOnly("group-a");
            Equal(true, document.ShowAllExceptSelection());
            Equal(false, document.Parts[0].Visible);
            Equal(false, document.Parts[1].Visible);
            Equal(true, document.Parts[2].Visible);
            Equal(true, document.Undo());
            Equal(false, document.IsDirty);
        }

        private static void BlueprintGroupsInheritVisibilityAndLock()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();

            Equal(true, document.SetVisibility("group-a", false));
            Equal(false, document.IsEffectivelyVisible("part-a"));
            Equal(true, document.Undo());
            Equal(true, document.IsEffectivelyVisible("part-a"));
            Equal(false, document.IsDirty);

            Equal(true, document.SetLocked("group-a", true));
            Equal(true, document.IsEffectivelyLocked("part-a"));
            Equal(false, document.ApplyTransformDelta(
                new Point3(1.0, 0.0, 0.0),
                IdentityRotation(),
                new Point3(0.0, 0.0, 0.0)));
        }

        private static void BlueprintNestedGroupsShareSelectionAndState()
        {
            var groups = new[]
            {
                new BlueprintEditorGroup("outer", "Outer"),
                new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer")
            };
            var parts = new[]
            {
                new BlueprintEditorPart("part-a", "prefab-a", "Part A",
                    new Point3(1.0, 0.0, 0.0), IdentityRotation(), "inner")
            };
            var document = new BlueprintEditorDocument(
                null, "Blueprint", "Other", parts, groups);

            document.SelectOnly("outer");
            Equal(true, document.IsPartSelected("part-a"));
            Equal(1, document.EditablePartSelectionCount);
            Equal(true, document.ApplyTransformDelta(
                new Point3(2.0, 0.0, 0.0), IdentityRotation(),
                new Point3(0.0, 0.0, 0.0)));
            Near(new Point3(3.0, 0.0, 0.0), document.Parts[0].Position);

            Equal(true, document.SetVisibility("outer", false));
            Equal(false, document.IsEffectivelyVisible("inner"));
            Equal(false, document.IsEffectivelyVisible("part-a"));
            Equal(true, document.SetLocked("outer", true));
            Equal(true, document.IsEffectivelyLocked("inner"));
            Equal(true, document.IsEffectivelyLocked("part-a"));
        }

        private static void BlueprintNestedGroupsMoveWithoutCycles()
        {
            var groups = new[]
            {
                new BlueprintEditorGroup("outer", "Outer"),
                new BlueprintEditorGroup("left", "Left", parentGroupId: "outer"),
                new BlueprintEditorGroup("right", "Right", parentGroupId: "outer")
            };
            var parts = new[]
            {
                new BlueprintEditorPart("part-a", "prefab-a", "Part A",
                    new Point3(0.0, 0.0, 0.0), IdentityRotation(), "left"),
                new BlueprintEditorPart("part-b", "prefab-b", "Part B",
                    new Point3(1.0, 0.0, 0.0), IdentityRotation(), "right")
            };
            var document = new BlueprintEditorDocument(
                null, "Blueprint", "Other", parts, groups);

            document.SelectOnly("left");
            document.ToggleSelection("right");
            Equal(true, document.CreateGroup("nested", "Nested"));
            Equal("outer", document.Groups[3].ParentGroupId);
            Equal("nested", document.Groups[1].ParentGroupId);
            Equal("nested", document.Groups[2].ParentGroupId);

            document.SelectOnly("outer");
            Equal(false, document.SetSelectionGroup("left"));
            Equal<string>(null, document.Groups[0].ParentGroupId);

            document.SelectOnly("nested");
            Equal(true, document.UngroupSelection());
            Equal(3, document.Groups.Count);
            Equal("outer", document.Groups[1].ParentGroupId);
            Equal("outer", document.Groups[2].ParentGroupId);

            Equal(true, document.Undo());
            document.SelectOnly("outer");
            Equal(true, document.DeleteSelection(false));
            Equal(3, document.Groups.Count);
            Equal<string>(null, document.Groups[2].ParentGroupId);
            Equal(2, document.Parts.Count);
            Equal(true, document.Undo());
            document.SelectOnly("outer");
            Equal(true, document.DeleteSelection(true));
            Equal(0, document.Groups.Count);
            Equal(0, document.Parts.Count);

            Throws<ArgumentException>(() => new BlueprintEditorDocument(
                null, "Blueprint", "Other", null,
                new[]
                {
                    new BlueprintEditorGroup("a", "A", parentGroupId: "b"),
                    new BlueprintEditorGroup("b", "B", parentGroupId: "a")
                }));
        }

        private static void BlueprintGroupDropIsAtomic()
        {
            var document = new BlueprintEditorDocument(null, "Group drop", "Other", new[]
            {
                new BlueprintEditorPart("a", "prefab-a", "A", new Point3(2, 3, 4),
                    new Rotation3(0, Math.Sqrt(0.5), 0, Math.Sqrt(0.5)), "inner",
                    scale: new Point3(0.01, 2, 0.5)),
                new BlueprintEditorPart("b", "prefab-b", "B", new Point3(-4, 5, 6),
                    IdentityRotation(), scale: new Point3(3, 0.5, 1)),
                new BlueprintEditorPart("locked", "prefab-c", "Locked", new Point3(7, 8, 9),
                    IdentityRotation(), locked: true)
            }, new[]
            {
                new BlueprintEditorGroup("outer", "Outer"),
                new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer"),
                new BlueprintEditorGroup("target", "Target"),
                new BlueprintEditorGroup("locked-target", "Locked target", locked: true)
            });
            List<BlueprintEditorPart> before = document.CopyParts();
            document.SelectOnly("outer");
            document.ToggleSelection("inner");
            document.ToggleSelection("a");
            document.ToggleSelection("b");
            document.ToggleSelection("locked");
            Equal(false, document.SetSelectionGroup("inner"));
            Equal(false, document.SetSelectionGroup("outer"));
            Equal(false, document.SetSelectionGroup("locked-target"));
            Equal(false, document.SetSelectionGroup("missing"));
            Equal(false, document.SetSelectionGroup(null));
            Equal(false, document.CanUndo);
            Equal(false, document.IsDirty);
            Equal<string>(null, document.Groups[0].ParentGroupId);
            Equal("outer", document.Groups[1].ParentGroupId);
            Equal<string>(null, document.Parts[1].ParentGroupId);

            Equal(true, document.SetSelectionGroup("target"));
            Equal("target", document.Groups[0].ParentGroupId);
            Equal("outer", document.Groups[1].ParentGroupId);
            Equal("inner", document.Parts[0].ParentGroupId);
            Equal("target", document.Parts[1].ParentGroupId);
            Equal<string>(null, document.Parts[2].ParentGroupId);
            for (int index = 0; index < before.Count; ++index)
            {
                Near(before[index].Position, document.Parts[index].Position);
                Near(before[index].Scale, document.Parts[index].Scale);
                Near(before[index].Rotation.X, document.Parts[index].Rotation.X);
                Near(before[index].Rotation.Y, document.Parts[index].Rotation.Y);
                Near(before[index].Rotation.Z, document.Parts[index].Rotation.Z);
                Near(before[index].Rotation.W, document.Parts[index].Rotation.W);
            }
            Equal(false, document.SetSelectionGroup("target"));
            Equal(true, document.Undo());
            Equal(false, document.CanUndo);
            Equal(false, document.IsDirty);
            Equal<string>(null, document.Groups[0].ParentGroupId);
            Equal<string>(null, document.Parts[1].ParentGroupId);
            Equal(false, document.SetSelectionGroup("inner"));
            Equal(true, document.CanRedo);

            // Controller rolls back a provisional document edit if Scene sync fails.
            Equal(true, document.SetSelectionGroup("target"));
            Equal(true, document.RollbackLastEdit());
            Equal<string>(null, document.Groups[0].ParentGroupId);
            Equal<string>(null, document.Parts[1].ParentGroupId);
            Equal(false, document.CanUndo);
            Equal(true, document.CanRedo);
            Equal(true, document.Redo());
            Equal("target", document.Groups[0].ParentGroupId);
            Equal("outer", document.Groups[1].ParentGroupId);
            Equal("target", document.Parts[1].ParentGroupId);
        }

        private static void PrecisionStepPresetsStaySharedWithF9()
        {
            float[] translation = { .01f, .05f, .1f, .5f, 1f };
            float[] rotation = { .1f, .5f, 1f, 5f, 15f };
            Equal(translation.Length, PrecisionStepPresets.Translation.Count);
            Equal(rotation.Length, PrecisionStepPresets.Rotation.Count);
            for (int index = 0; index < translation.Length; ++index)
                Equal(translation[index], PrecisionStepPresets.Translation[index]);
            for (int index = 0; index < rotation.Length; ++index)
                Equal(rotation[index], PrecisionStepPresets.Rotation[index]);
        }

        private static void BlueprintGroupTransformAppliesEachPartOnce()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.ToggleSelection("part-a");

            double halfRoot = Math.Sqrt(0.5);
            Equal(true, document.ApplyTransformDelta(
                new Point3(1.0, 0.0, 0.0),
                new Rotation3(0.0, 0.0, halfRoot, halfRoot),
                new Point3(0.0, 0.0, 0.0)));

            Near(new Point3(1.0, 1.0, 0.0), document.Parts[0].Position);
            Near(new Point3(0.0, 0.0, 0.0), document.Parts[1].Position);
        }

        private static void BlueprintScaleIsAtomicAndBounded()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 2.0, 0.0, 0.0));
            document.SelectOnly("part-a");
            document.MarkClean();

            Equal(true, document.ApplyTransformDelta(
                new Point3(0.0, 0.0, 0.0),
                IdentityRotation(),
                new Point3(0.0, 0.0, 0.0),
                1.5));
            Near(new Point3(3.0, 0.0, 0.0), document.Parts[0].Position);
            Near(new Point3(1.5, 1.5, 1.5), document.Parts[0].Scale);
            Equal(true, document.Undo());
            Near(new Point3(2.0, 0.0, 0.0), document.Parts[0].Position);
            Near(new Point3(1.0, 1.0, 1.0), document.Parts[0].Scale);
            Equal(false, document.IsDirty);

            Throws<ArgumentOutOfRangeException>(() => document.ApplyTransformDelta(
                new Point3(0.0, 0.0, 0.0),
                IdentityRotation(),
                new Point3(0.0, 0.0, 0.0),
                4.01));
            Near(new Point3(2.0, 0.0, 0.0), document.Parts[0].Position);
            Near(new Point3(1.0, 1.0, 1.0), document.Parts[0].Scale);
        }

        private static void BlueprintScaleAbsorbsOnlyBoundaryRoundoff()
        {
            double floatMinimum = (double)0.01f;
            var fromStore = new BlueprintEditorPart("stored", "woodwall", "Stored", new Point3(),
                IdentityRotation(), scale: new Point3(floatMinimum, 0.010001, 4));
            Equal(0.01, fromStore.Scale.X);
            Equal(0.010001, fromStore.Scale.Y);
            Equal(4.0, fromStore.Scale.Z);

            var document = EditorDocument(new BlueprintEditorPart("a", "woodwall", "A",
                new Point3(6, 0, 0), IdentityRotation(), scale: new Point3(3, 3, 3)));
            document.SelectOnly("a");
            Equal(true, document.ApplyTransformDelta(new Point3(), IdentityRotation(),
                new Point3(), (double)(4f / 3f)));
            Equal(4.0, document.Parts[0].Scale.X);
            Equal(true, document.ApplyTransformDelta(new Point3(), IdentityRotation(),
                new Point3(), (double)(0.01f / 4f)));
            Equal(0.01, document.Parts[0].Scale.X);
            Equal(true, document.SetPartProperties("a", "A", new Point3(6, 0, 0),
                IdentityRotation(), new Point3(floatMinimum, 1, 1)));
            Equal(0.01, document.Parts[0].Scale.X);

            var array = EditorDocument(new BlueprintEditorPart("source", "woodwall", "Source",
                new Point3(), IdentityRotation(), scale: new Point3(3, 3, 3)));
            array.SelectOnly("source");
            var preview = array.PreviewArray(2, 1, new Point3(1, 0, 0), new Point3(0, 0, 1), new Point3(0, 1, 0), 0, 0, false, (double)(1f / 3f), (new Point3(1, 0, 0)) * -0.5, (new Point3(1, 0, 0)) * 0.5);
            Equal(4.0, preview[0].Scale.X);

            foreach (double invalid in new[] { 0.00999, 4.00001, 0.01 * (1 - 3e-7),
                4 * (1 + 3e-7), double.NaN, double.PositiveInfinity })
            {
                Throws<ArgumentOutOfRangeException>(() => new BlueprintEditorPart("bad", "woodwall", "Bad",
                    new Point3(), IdentityRotation(), scale: new Point3(invalid, 1, 1)));
                var atomic = EditorDocument(EditorPart("a", 2, 0, 0));
                atomic.SelectOnly("a");
                atomic.MarkClean();
                Throws<ArgumentOutOfRangeException>(() => atomic.SetPartProperties("a", "Changed",
                    new Point3(9, 0, 0), IdentityRotation(), new Point3(invalid, 1, 1)));
                Throws<ArgumentOutOfRangeException>(() => atomic.ApplyTransformDelta(
                    new Point3(5, 0, 0), IdentityRotation(), new Point3(), invalid));
                Near(new Point3(2, 0, 0), atomic.Parts[0].Position);
                Near(new Point3(1, 1, 1), atomic.Parts[0].Scale);
                Equal(false, atomic.IsDirty);
                Equal(false, atomic.CanUndo);
            }
        }

        private static void BlueprintGroupResetPreservesArrangementAndIsAtomic()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();
            Point3 beforeDelta = document.Parts[1].Position - document.Parts[0].Position;
            var pivot = new Point3(0.5, 0.5, 0.0);

            Equal(true, document.ApplyTransformDelta(
                new Point3(-0.5, -0.5, 0.0),
                IdentityRotation(),
                pivot));

            Near(new Point3(0.5, -0.5, 0.0), document.Parts[0].Position);
            Near(new Point3(-0.5, 0.5, 0.0), document.Parts[1].Position);
            Near(beforeDelta, document.Parts[1].Position - document.Parts[0].Position);
            Equal(true, document.Undo());
            Near(new Point3(1.0, 0.0, 0.0), document.Parts[0].Position);
            Near(new Point3(0.0, 1.0, 0.0), document.Parts[1].Position);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintHistoryRestoresCleanRevisions()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");
            document.MarkClean();

            Equal(true, document.Rename("part-a", "Renamed"));
            Equal(true, document.IsDirty);
            Equal(true, document.CanUndo);
            Equal(true, document.Undo());
            Equal("Part part-a", document.Parts[0].DisplayName);
            Equal(false, document.IsDirty);
            Equal(true, document.Redo());
            Equal("Renamed", document.Parts[0].DisplayName);
            Equal(true, document.IsDirty);
        }

        private static void BlueprintGroupDeletionReparentsOrRemovesChildren()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            Equal(true, document.DeleteSelection(false));
            Equal(0, document.Groups.Count);
            Equal(2, document.Parts.Count);
            Equal<string>(null, document.Parts[0].ParentGroupId);
            Equal<string>(null, document.Parts[1].ParentGroupId);

            Equal(true, document.Undo());
            Equal(true, document.DeleteSelection(true));
            Equal(0, document.Groups.Count);
            Equal(0, document.Parts.Count);
        }

        private static void BlueprintDocumentRejectsInvalidGraphs()
        {
            BlueprintEditorPart valid = EditorPart("part-a", 0.0, 0.0, 0.0);
            Throws<ArgumentException>(() => EditorDocument(
                valid,
                EditorPart("part-a", 1.0, 0.0, 0.0)));
            Throws<ArgumentException>(() => new BlueprintEditorDocument(
                null,
                "Blueprint",
                "Other",
                new[] { new BlueprintEditorPart(
                    "part-a",
                    "prefab-a",
                    "Part",
                    new Point3(0.0, 0.0, 0.0),
                    IdentityRotation(),
                    "missing-group") }));
            Throws<ArgumentOutOfRangeException>(() => EditorDocument(
                EditorPart("part-a", double.NaN, 0.0, 0.0)));
            Throws<ArgumentOutOfRangeException>(() => new BlueprintEditorPart(
                "part-a",
                "prefab-a",
                "Part",
                new Point3(0.0, 0.0, 0.0),
                new Rotation3(0.0, 0.0, 0.0, 0.0)));
        }

        private static void BlueprintFailedEditsRollBackAtomically()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", double.MaxValue, 0.0, 0.0));
            document.SelectOnly("part-a");
            document.MarkClean();

            Throws<ArgumentOutOfRangeException>(() => document.ApplyTransformDelta(
                new Point3(double.MaxValue, 0.0, 0.0),
                IdentityRotation(),
                new Point3(0.0, 0.0, 0.0)));

            Near(new Point3(double.MaxValue, 0.0, 0.0), document.Parts[0].Position);
            Equal(false, document.IsDirty);
            Equal(false, document.CanUndo);
        }

        private static void BlueprintCollectionViewsCannotMutateDocument()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            Equal(false, document.Parts is List<BlueprintEditorPart>);
            Equal(false, document.Groups is List<BlueprintEditorGroup>);
            Equal(false, document.Selection is List<string>);

            Throws<NotSupportedException>(() =>
                ((IList<BlueprintEditorPart>)document.Parts).Add(
                    EditorPart("part-c", 0.0, 0.0, 0.0)));
            Throws<NotSupportedException>(() =>
                ((IList<string>)document.Selection).Add("part-a"));
            Equal(2, document.Parts.Count);
            Equal(1, document.Groups.Count);
        }

        private static void BlueprintDuplicationIsOneAtomicEdit()
        {
            BlueprintEditorDocument document = GroupedEditorDocument();
            document.MarkClean();

            Equal(true, document.DuplicateSelection(new Point3(0.5, 0.0, 0.0)));
            Equal(4, document.Parts.Count);
            Equal(1, document.Selection.Count);
            Equal(2, document.Groups.Count);
            Equal(document.Selection[0], document.Parts[2].ParentGroupId);
            Near(new Point3(1.5, 0.0, 0.0), document.Parts[2].Position);
            Equal(true, document.IsDirty);

            Equal(true, document.Undo());
            Equal(2, document.Parts.Count);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintCopyDragIsAtomic()
        {
            var document = new BlueprintEditorDocument(null, "Copy", "Other", new[]
            {
                new BlueprintEditorPart("a", "wall", "A", new Point3(1, 0, 0),
                    new Rotation3(0, 0, 0, 1), "g"),
                new BlueprintEditorPart("b", "wall", "B", new Point3(2, 0, 0),
                    new Rotation3(0, 0, 0, 1), "g", visible: false, locked: true)
            }, new[] { new BlueprintEditorGroup("g", "Group") });
            document.SelectOnly("g");
            document.ToggleSelection("a");
            document.MarkClean();
            Equal(true, document.DuplicateSelection(new Point3(10, 0, 0)));
            Equal(4, document.Parts.Count);
            Equal(1, document.Selection.Count);
            Near(new Point3(1, 0, 0), document.Parts[0].Position);
            Near(new Point3(11, 0, 0), document.Parts[2].Position);
            Near(new Point3(12, 0, 0), document.Parts[3].Position);
            Near(new Point3(1, 1, 1), document.Parts[3].Scale);
            Equal(true, document.Parts[3].Locked);
            Equal(false, document.Parts[3].Visible);
            Equal(true, document.Undo());
            Equal(2, document.Parts.Count);
            Equal(false, document.CanUndo);
            Equal(false, document.IsDirty);
            Throws<ArgumentOutOfRangeException>(() => document.DuplicateSelection(new Point3(double.NaN, 0, 0)));
            Equal(2, document.Parts.Count);
            Equal(true, document.CanRedo);
        }

        private static void BlueprintInsertionIsAtomic()
        {
            var source = new BlueprintEditorDocument(null, "Imported", "Other", new[]
            {
                new BlueprintEditorPart("a", "wall", "A", new Point3(1, 0, 0),
                    new Rotation3(0, 0, 0, 1), "inner", scale: new Point3(2, 1, 0.5)),
                new BlueprintEditorPart("b", "wall", "B", new Point3(2, 0, 0),
                    new Rotation3(0, 0, 0, 1), visible: false, locked: true)
            }, new[] { new BlueprintEditorGroup("outer", "Outer"),
                new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer") });
            var document = EditorDocument(EditorPart("a", 0, 0, 0));
            Equal(true, document.InsertBlueprint(source, new Point3(10, 0, 0), new Rotation3(0, 0, 1, 0)));
            Equal(3, document.Parts.Count);
            Equal(3, document.Groups.Count);
            Equal(document.Selection[0], document.Groups[1].ParentGroupId);
            Equal(document.Groups[1].StableId, document.Groups[2].ParentGroupId);
            Equal(document.Groups[2].StableId, document.Parts[1].ParentGroupId);
            Equal(document.Selection[0], document.Parts[2].ParentGroupId);
            Equal(false, document.Parts[1].StableId == "a");
            Near(new Point3(9, 0, 0), document.Parts[1].Position);
            Near(new Point3(2, 1, 0.5), document.Parts[1].Scale);
            Near(new Point3(1, 0, 0), source.Parts[0].Position);
            Equal(true, document.Parts[2].Locked);
            Equal(false, document.Parts[2].Visible);
            Equal(true, document.Undo());
            Equal(1, document.Parts.Count);
            Equal(0, document.Groups.Count);
            Equal(false, document.CanUndo);
            var full = new List<BlueprintEditorPart>();
            for (int i = 0; i < BlueprintEditorDocument.MaximumParts - 1; ++i)
                full.Add(EditorPart("p" + i, i, 0, 0));
            var limited = new BlueprintEditorDocument(null, "Full", "Other", full);
            Throws<InvalidOperationException>(() => limited.InsertBlueprint(source,
                new Point3(), new Rotation3(0, 0, 0, 1)));
            Equal(BlueprintEditorDocument.MaximumParts - 1, limited.Parts.Count);
            Equal(0, limited.Groups.Count);
            Equal(false, limited.CanUndo);
        }

        private static void BlueprintEmptyGroupIsUndoable()
        {
            var document = EditorDocument(EditorPart("a", 0, 0, 0));
            Equal(true, document.CreateEmptyGroup("g", "Empty"));
            Equal("g", document.ActiveNodeId);
            Equal(true, document.CreateEmptyGroup("child", "Child", "g"));
            Equal("g", document.Groups[1].ParentGroupId);
            Equal(true, document.Undo());
            Equal(true, document.Undo());
            Equal(0, document.Groups.Count);
            Equal(1, document.Parts.Count);
            Equal(false, document.CanUndo);
            Throws<ArgumentException>(() => document.CreateEmptyGroup("bad", "Bad", "missing"));
            Equal(0, document.Groups.Count);
        }

        private static void BlueprintNestedDuplicateIsIndependent()
        {
            var document = new BlueprintEditorDocument(null, "Nested", "Other",
                new[]
                {
                    new BlueprintEditorPart("a", "woodwall", "A", new Point3(1, 0, 0),
                        new Rotation3(0, 0, 0, 1), "inner"),
                    new BlueprintEditorPart("b", "woodwall", "B", new Point3(2, 0, 0),
                        new Rotation3(0, 0, 0, 1), "inner", visible: false, locked: true)
                },
                new[] { new BlueprintEditorGroup("outer", "Outer"),
                    new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer") });
            document.SelectOnly("outer");
            document.ToggleSelection("a");
            Equal(true, document.IsPartSelected("a", new[] { "outer" }));
            Equal(false, document.IsPartSelected("a", new[] { "b" }));
            Equal(true, document.DuplicateSelection(new Point3(10, 0, 0)));
            Equal(4, document.Groups.Count);
            Equal(4, document.Parts.Count);
            Equal(1, document.Selection.Count);
            Equal(document.Selection[0], document.Groups[3].ParentGroupId);
            Equal(document.Groups[3].StableId, document.Parts[2].ParentGroupId);
            Equal(false, document.Parts[3].Visible);
            Equal(true, document.Parts[3].Locked);
            Equal(true, document.ApplyTransformDelta(new Point3(1, 0, 0),
                new Rotation3(0, 0, 0, 1), new Point3()));
            Near(new Point3(1, 0, 0), document.Parts[0].Position);
            Near(new Point3(12, 0, 0), document.Parts[2].Position);
            Equal(true, document.Undo());
            Equal(true, document.Undo());
            Equal(2, document.Groups.Count);
            Equal(2, document.Parts.Count);
        }

        private static void BlueprintDuplicateLimitIsAtomic()
        {
            var groups = new List<BlueprintEditorGroup>();
            for (int index = 0; index < BlueprintEditorDocument.MaximumGroups; ++index)
                groups.Add(new BlueprintEditorGroup("g" + index, "Group " + index));
            var document = new BlueprintEditorDocument(null, "Limit", "Other",
                new[] { new BlueprintEditorPart("part", "woodwall", "Wall", new Point3(),
                    new Rotation3(0, 0, 0, 1), "g0") }, groups);
            document.SelectOnly("g0");
            document.MarkClean();
            bool rejected = false;
            try { document.DuplicateSelection(new Point3(1, 0, 0)); }
            catch (InvalidOperationException) { rejected = true; }
            Equal(true, rejected);
            Equal(BlueprintEditorDocument.MaximumGroups, document.Groups.Count);
            Equal(1, document.Parts.Count);
            Equal("g0", document.Selection[0]);
            Equal(false, document.IsDirty);
            Equal(false, document.CanUndo);
        }

        private static void BlueprintArrayPreviewMatchesApply()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");
            document.MarkClean();

            IReadOnlyList<BlueprintEditorPart> preview = document.PreviewArray(3, 1, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 2.0), new Point3(0, 1, 0), 90.0, 0, false, -0.10, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5);
            Equal(2, preview.Count);
            Equal(1, document.Parts.Count);
            Equal(false, document.IsDirty);
            Near(new Point3(.5, 0.0, -.45), preview[0].Position);
            Near(new Point3(.1, 0.0, -.9), preview[1].Position);
            Near(new Point3(0.9, 0.9, 0.9), preview[0].Scale);
            Near(new Point3(0.8, 0.8, 0.8), preview[1].Scale);
            double halfRoot = Math.Sqrt(0.5);
            Near(halfRoot, preview[0].Rotation.Y);
            Near(halfRoot, preview[0].Rotation.W);
            Near(1.0, preview[1].Rotation.Y);
            Near(0.0, preview[1].Rotation.W);

            Equal(true, document.ApplyArray(3, 1, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 2.0), new Point3(0, 1, 0), 90.0, 0, false, -0.10, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5));
            Equal(3, document.Parts.Count);
            Equal(2, document.Selection.Count);
            Near(preview[1].Position, document.Parts[2].Position);
            Near(preview[1].Scale, document.Parts[2].Scale);
            Equal(true, document.Undo());
            Equal(1, document.Parts.Count);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintArrayEnforcesLimits()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");

            Throws<ArgumentOutOfRangeException>(() => document.PreviewArray(4, 1, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 1.0), new Point3(0, 1, 0), 0.0, 0, false, -0.50, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5));
            Near(.995, document.PreviewArray(2, 1, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 1.0), new Point3(0, 1, 0), 0.0, 0, false, -0.005, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5)[0].Scale.X);
            Equal(127, document.PreviewArray(128, 1, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 1.0), new Point3(0, 1, 0), 0.0, 0, false, 0.0, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5).Count);
            Throws<InvalidOperationException>(() => document.ApplyArray(128, 2, new Point3(1.0, 0.0, 0.0), new Point3(0.0, 0.0, 1.0), new Point3(0, 1, 0), 0.0, 0, false, 0.0, (new Point3(1.0, 0.0, 0.0)) * -0.5, (new Point3(1.0, 0.0, 0.0)) * 0.5));
            Equal(1, document.Parts.Count);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintArrayCountsIncludeSource()
        {
            BlueprintEditorDocument document = EditorDocument(EditorPart("source", 0, 0, 0));
            document.SelectOnly("source");
            Point3 first = new Point3(1,0,0), second = new Point3(0,0,-2);
            Equal(0, document.PreviewArray(1, 1, first, second, new Point3(0, 1, 0), 0, 0, false, 0, (first) * -0.5, (first) * 0.5).Count);
            Equal(false, document.ApplyArray(1, 1, first, second, new Point3(0, 1, 0), 0, 0, false, 0, (first) * -0.5, (first) * 0.5));
            Equal(false, document.CanUndo);
            IReadOnlyList<BlueprintEditorPart> preview = document.PreviewArray(3, 2, first, second, new Point3(0, 1, 0), 0, 0, false, 0.1, (first) * -0.5, (first) * 0.5);
            Equal(5, preview.Count);
            Near(new Point3(1.05,0,0), preview[0].Position);
            Near(new Point3(2.2,0,0), preview[1].Position);
            Near(new Point3(0,0,-2), preview[2].Position);
            Near(new Point3(1.05,0,-2), preview[3].Position);
            Near(new Point3(2.2,0,-2), preview[4].Position);
            Near(new Point3(1.1,1.1,1.1), preview[0].Scale);
            Near(new Point3(1,1,1), preview[2].Scale);
            Near(new Point3(1.2,1.2,1.2), preview[4].Scale);
            Equal(true, document.ApplyArray(3, 2, first, second, new Point3(0, 1, 0), 0, 0, false, 0.1, (first) * -0.5, (first) * 0.5));
            Equal(6, document.Parts.Count);
            Near(new Point3(), document.Parts[0].Position);
            Near(new Point3(1,1,1), document.Parts[0].Scale);
            Equal(true, document.Undo());
            Equal(1, document.Parts.Count);
        }

        private static void RepeatScaleHinges()
        {
            Point3 axis = new Point3(0,1,0), step = new Point3(2.25,0,0);
            Point3 back = new Point3(-1,0,0), front = new Point3(1,0,0);
            foreach (double scaleStep in new[] { .1, -.1 })
            {
                var samples = GuidePathSampling.SampleRepeat(default, step, 5, .2, axis,
                    30, true, back, front, scaleStep);
                int[] logical = { 0,1,-1,2,-2 };
                for (int index = 0; index < samples.Count; ++index)
                {
                    Near(1 + logical[index]*scaleStep, samples[index].UniformScale);
                    Near(.2 * logical[index], samples[index].Position.Y);
                }
                var negative = samples[2];
                double negativeHalf = negative.IncrementalRotationDegrees * Math.PI / 360;
                var negativeRotation = new Rotation3(0,Math.Sin(negativeHalf),0,Math.Cos(negativeHalf));
                Near(negative.Position + negativeRotation.Rotate(front*negative.UniformScale + new Point3(.25,0,0)) +
                    new Point3(0,.2,0), samples[0].Position + back);
                foreach (int next in new[] { 1,3 })
                {
                    int previous = next == 1 ? 0 : 1;
                    var a = samples[previous]; var b = samples[next];
                    double halfA = a.IncrementalRotationDegrees * Math.PI / 360;
                    double halfB = b.IncrementalRotationDegrees * Math.PI / 360;
                    var ra = new Rotation3(0,Math.Sin(halfA),0,Math.Cos(halfA));
                    var rb = new Rotation3(0,Math.Sin(halfB),0,Math.Cos(halfB));
                    Near(a.Position + ra.Rotate(front*a.UniformScale + new Point3(.25,0,0)) + new Point3(0,.2,0),
                        b.Position + rb.Rotate(back*b.UniformScale));
                }
            }
            Throws<ArgumentOutOfRangeException>(() => GuidePathSampling.SampleRepeat(default,
                step, 4, 0, axis, 0, false, back, front, -.5));
            Throws<ArgumentOutOfRangeException>(() => GuidePathSampling.SampleRepeat(default,
                step, 5, 0, axis, 0, true, back, front, 1));
            BlueprintEditorDocument doc = EditorDocument(EditorPart("p",0,0,0)); doc.SelectOnly("p");
            Near(1.001, doc.PreviewArray(2,1,step,default,axis,0,0,false,.001,back,front)[0].Scale.X);
            Throws<ArgumentOutOfRangeException>(() => doc.PreviewArray(1,2,step,step,axis,double.NaN,0,false,0,back,front));
            Throws<ArgumentOutOfRangeException>(() => doc.PreviewArray(1,2,step,step,axis,0,double.NaN,false,0,back,front));
            Throws<ArgumentOutOfRangeException>(() => doc.PreviewArray(1,2,step,step,axis,0,0,false,0,new Point3(double.NaN,0,0),front));
            var preview = doc.PreviewArray(3,3,step,new Point3(0,0,4),axis,30,.2,true,.1,back,front);
            Equal(8, preview.Count);
            Near(preview[0].Position + new Point3(0,0,4), preview[3].Position);
            Near(preview[0].Scale, preview[3].Scale);
            Near(new Point3(0,0,-4), preview[5].Position);
            Equal(true, doc.ApplyArray(3,3,step,new Point3(0,0,4),axis,30,.2,true,.1,back,front));
            Near(preview[7].Position, doc.Parts[8].Position);
            Equal(true, doc.Undo()); Equal(1, doc.Parts.Count);
        }

        private static void BlueprintContourPreviewMatchesApply()
        {
            var source = new BlueprintEditorPart(
                "source", "beam", "Beam",
                new Point3(0.0, 1.0, 0.5), IdentityRotation());
            var left = new BlueprintEditorPart(
                "left", "support", "Left",
                new Point3(-2.0, 0.0, 0.0), IdentityRotation());
            var center = new BlueprintEditorPart(
                "center", "support", "Center",
                new Point3(0.0, 0.0, 0.0), IdentityRotation());
            double halfRoot = Math.Sqrt(0.5);
            var right = new BlueprintEditorPart(
                "right", "support", "Right",
                new Point3(2.0, 0.0, 0.0),
                new Rotation3(0.0, halfRoot, 0.0, halfRoot));
            BlueprintEditorDocument document = EditorDocument(source, left, center, right);
            document.SelectOnly("source");
            document.MarkClean();
            string[] supports = { "left", "center", "right" };

            IReadOnlyList<BlueprintEditorPart> preview = document.PreviewContour(
                supports, false, -0.10);
            Equal(2, preview.Count);
            Equal(4, document.Parts.Count);
            Equal(false, document.IsDirty);
            Near(new Point3(-2.0, 1.0, 0.5), preview[0].Position);
            Near(new Point3(2.5, 1.0, 0.0), preview[1].Position);
            Near(new Point3(0.9, 0.9, 0.9), preview[1].Scale);
            Near(halfRoot, preview[1].Rotation.Y);
            Near(halfRoot, preview[1].Rotation.W);

            Equal(true, document.ApplyContour(
                supports, false, -0.10));
            Equal(6, document.Parts.Count);
            Equal(2, document.Selection.Count);
            Near(preview[1].Position, document.Parts[5].Position);
            Near(preview[1].Scale, document.Parts[5].Scale);
            Equal(true, document.Undo());
            Equal(4, document.Parts.Count);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintContourEnforcesLimits()
        {
            var parts = new List<BlueprintEditorPart>
            {
                new BlueprintEditorPart("source", "beam", "Beam",
                    new Point3(2.0, 1.0, 0.0), IdentityRotation())
            };
            var supports = new List<string>();
            for (int index = 0; index < 5; ++index)
            {
                string id = "support-" + index;
                supports.Add(id);
                parts.Add(new BlueprintEditorPart(id, "support", id,
                    new Point3(index, 0.0, 0.0), IdentityRotation()));
            }
            BlueprintEditorDocument document = EditorDocument(parts.ToArray());
            document.SelectOnly("source");

            Equal(2, document.PreviewContour(supports, false, -0.50).Count);
            Equal(4, document.PreviewContour(
                supports, false, (double)(float)-0.01).Count);
            Throws<ArgumentOutOfRangeException>(() => document.PreviewContour(
                supports, false, -0.005));
            Throws<ArgumentException>(() => document.PreviewContour(
                new[] { "support-0", "support-0" }, false, 0.0));
            document.SelectOnly("support-0");
            Throws<InvalidOperationException>(() => document.PreviewContour(
                new[] { "support-0", "support-1" }, false, 0.0));
            Equal(6, document.Parts.Count);
            Equal(false, document.IsDirty);
        }

        private static void BlueprintRollbackDropsFailedEdit()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");
            document.MarkClean();

            Equal(true, document.Rename("part-a", "Failed scene edit"));
            Equal(true, document.RollbackLastEdit());
            Equal("Part part-a", document.Parts[0].DisplayName);
            Equal(false, document.IsDirty);
            Equal(false, document.CanUndo);
            Equal(false, document.CanRedo);
        }

        private static void BlueprintRollbackPreservesPriorRedo()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");

            Equal(true, document.Rename("part-a", "First edit"));
            document.AcceptLastEdit();
            Equal(true, document.Undo());
            Equal(true, document.CanRedo);

            Equal(true, document.Rename("part-a", "Failed scene edit"));
            Equal(false, document.CanRedo);
            Equal(true, document.RollbackLastEdit());
            Equal(true, document.CanRedo);
            Equal("Part part-a", document.Parts[0].DisplayName);

            Equal(true, document.Redo());
            Equal("First edit", document.Parts[0].DisplayName);
        }

        private static void BlueprintAdoptsSavedIdentity()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));

            document.AdoptSavedIdentity("saved-id", "Saved name", "Saved category");

            Equal("saved-id", document.SourceBlueprintId);
            Equal("Saved name", document.Name);
            Equal("Saved category", document.Category);
            Equal(false, document.IsDirty);
            Equal(false, document.CanUndo);
        }

        private static void BlueprintInspectorEditIsAtomic()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.MarkClean();

            Equal(true, document.SetPartProperties(
                "part-a",
                "Edited",
                new Point3(1.0, 2.0, 3.0),
                new Rotation3(0.0, 0.0, 1.0, 1.0)));
            Equal("Edited", document.Parts[0].DisplayName);
            Near(new Point3(1.0, 2.0, 3.0), document.Parts[0].Position);
            Equal(true, document.Undo());
            Equal("Part part-a", document.Parts[0].DisplayName);
            Near(new Point3(0.0, 0.0, 0.0), document.Parts[0].Position);
            Equal(false, document.IsDirty);

            Equal(true, document.SetLocked("part-a", true));
            Equal(false, document.SetPartProperties(
                "part-a", "Blocked", new Point3(4.0, 0.0, 0.0), IdentityRotation()));
        }

        private static void BlueprintMetadataEditIsAtomic()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.MarkClean();

            Equal(true, document.SetMetadata("Bridge", "wood"));
            Equal("Bridge", document.Name);
            Equal("WOOD", document.Category);
            Equal(true, document.IsDirty);
            Equal(true, document.Undo());
            Equal("Blueprint", document.Name);
            Equal("Other", document.Category);
            Equal(false, document.IsDirty);
            Equal(true, document.Redo());
            Equal("Bridge", document.Name);
            Equal("WOOD", document.Category);
        }

        private static void BlueprintLimitsAndHistoryAreBounded()
        {
            var tooManyParts = new List<BlueprintEditorPart>();
            for (int index = 0; index <= BlueprintEditorDocument.MaximumParts; ++index)
                tooManyParts.Add(EditorPart("part-" + index, index, 0.0, 0.0));
            Throws<ArgumentOutOfRangeException>(() => new BlueprintEditorDocument(
                null, "Blueprint", "Other", tooManyParts));

            var tooManyGroups = new List<BlueprintEditorGroup>();
            for (int index = 0; index <= BlueprintEditorDocument.MaximumGroups; ++index)
                tooManyGroups.Add(new BlueprintEditorGroup("group-" + index, "Group"));
            Throws<ArgumentOutOfRangeException>(() => new BlueprintEditorDocument(
                null, "Blueprint", "Other", null, tooManyGroups));

            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0));
            document.SelectOnly("part-a");
            for (int index = 0; index < 40; ++index)
                Equal(true, document.Rename("part-a", "Part " + index));
            int undoCount = 0;
            while (document.Undo()) ++undoCount;
            Equal(BlueprintEditorDocument.MaximumHistory, undoCount);

            Equal(true, document.Redo());
            Equal(true, document.Rename("part-a", "New branch"));
            Equal(false, document.CanRedo);
        }

        private static void BlueprintLayoutStaysInsideSafeArea()
        {
            BlueprintEditorLayout enlarged = BlueprintEditorLayout.Compute(1920.0 / 1.4, 1080.0 / 1.4);
            Equal(true, (enlarged.Outliner.Height - BlueprintEditorLayout.OutlinerHeaderHeight) /
                BlueprintEditorLayout.OutlinerRowHeight >= 2);
            Equal(true, enlarged.Inspector.Height - 50.0 >= 390.0);
            BlueprintEditorLayout smallest = BlueprintEditorLayout.Compute(640.0, 480.0);
            Equal(true, smallest.SafeArea.Contains(smallest.Outliner));
            Equal(true, smallest.SafeArea.Contains(smallest.Inspector));
            Equal(true, smallest.SafeArea.Contains(smallest.Viewport));
            Equal(false, smallest.Outliner.Overlaps(smallest.Inspector));
            Equal(false, smallest.Viewport.Overlaps(smallest.RightColumn));
            double[,] cases =
            {
                { 1920.0, 1080.0 },
                { 3440.0, 1440.0 },
                { 1280.0, 720.0 }
            };
            double[] scales = { 0.8, 1.0, 1.2, 1.4 };
            for (int resolution = 0; resolution < cases.GetLength(0); ++resolution)
            {
                foreach (double scale in scales)
                {
                    BlueprintEditorLayout layout = BlueprintEditorLayout.Compute(
                        cases[resolution, 0] / scale,
                        cases[resolution, 1] / scale);
                    Equal(true, layout.SafeArea.Contains(layout.Top));
                    Equal(true, layout.SafeArea.Contains(layout.Rail));
                    Equal(true, layout.SafeArea.Contains(layout.Viewport));
                    Equal(true, layout.SafeArea.Contains(layout.RightColumn));
                    Equal(true, layout.SafeArea.Contains(layout.Status));
                    Equal(false, layout.Top.Overlaps(layout.Viewport));
                    Equal(false, layout.Rail.Overlaps(layout.Viewport));
                    Equal(false, layout.Viewport.Overlaps(layout.RightColumn));
                    Equal(false, layout.Status.Overlaps(layout.Viewport));
                    Near(0.0,
                        (layout.Outliner.Height - BlueprintEditorLayout.OutlinerHeaderHeight) %
                        BlueprintEditorLayout.OutlinerRowHeight);
                    Near(layout.RightColumn.Height,
                        layout.Outliner.Height + layout.Inspector.Height);
                    if (!layout.Compact && layout.RightColumn.Height >= BlueprintEditorLayout.MinimumInspectorHeight +
                        BlueprintEditorLayout.OutlinerHeaderHeight + BlueprintEditorLayout.OutlinerRowHeight)
                        Equal(true, layout.Inspector.Height >= BlueprintEditorLayout.MinimumInspectorHeight);
                }
            }
        }

        private static void BlueprintCatalogUsesWholePages()
        {
            Equal(12, BlueprintEditorLayout.CatalogColumns);
            Equal(4, BlueprintEditorLayout.CatalogRows);
            Equal(48, BlueprintEditorLayout.CatalogPageSize);
            Near(1052.0, BlueprintEditorLayout.CatalogGridWidth);
            Near(396.0, BlueprintEditorLayout.CatalogGridHeight);
        }

        private static void BlueprintArrayUsesResolvedGroupPivot()
        {
            var document = new BlueprintEditorDocument(null, "Pivot array", "Other",
                new[]
                {
                    new BlueprintEditorPart("pivot", "wall", "Pivot", default,
                        IdentityRotation(), "group"),
                    new BlueprintEditorPart("far", "wall", "Far", new Point3(4, 0, 0),
                        IdentityRotation(), "group")
                },
                new[] { new BlueprintEditorGroup("group", "Group", pivotPartId: "pivot") });
            document.SelectOnly("group");
            Point3 step = new Point3(10, 0, 0);
            IReadOnlyList<BlueprintEditorPart> preview = document.PreviewArray(
                2, 1, step, default, new Point3(0, 1, 0), 0, 0, false, 1,
                step * -0.5, step * 0.5, default(Point3));
            Equal(2, preview.Count);
            Near(new Point3(15, 0, 0), preview[0].Position);
            Near(new Point3(23, 0, 0), preview[1].Position);
            Equal(true, document.ApplyArray(
                2, 1, step, default, new Point3(0, 1, 0), 0, 0, false, 1,
                step * -0.5, step * 0.5, default(Point3)));
            Near(preview[0].Position, document.Parts[2].Position);
            Near(preview[1].Position, document.Parts[3].Position);
        }

        private static void BlueprintPrimaryPartIsOptionalAndUndoable()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 0.0, 0.0, 0.0),
                EditorPart("part-b", 2.0, 0.0, 0.0));
            Equal(null, document.PrimaryPartId);
            Equal(false, document.SetPrimaryPart("missing"));
            Equal(true, document.SetPrimaryPart("part-b"));
            Equal("part-b", document.PrimaryPartId);
            Equal(true, document.Undo());
            Equal(null, document.PrimaryPartId);
            Equal(true, document.Redo());
            Equal("part-b", document.PrimaryPartId);
            document.SelectOnly("part-b");
            Equal(true, document.DeleteSelection(false));
            Equal(null, document.PrimaryPartId);
            Equal(true, document.Undo());
            Equal("part-b", document.PrimaryPartId);
            Throws<ArgumentException>(() => new BlueprintEditorDocument(
                null, "Invalid", "Other", document.Parts, null, "missing"));
        }

        private static void BlueprintPrimaryGroupIsOptionalAndUndoable()
        {
            var document = new BlueprintEditorDocument(null, "Primary group", "Other",
                new[]
                {
                    new BlueprintEditorPart("part-a", "wall", "A", default,
                        IdentityRotation(), "inner"),
                    EditorPart("part-b", 2.0, 0.0, 0.0)
                },
                new[]
                {
                    new BlueprintEditorGroup("outer", "Outer"),
                    new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer")
                });
            Equal(false, document.SetPrimaryGroup("missing"));
            Equal(true, document.CreateEmptyGroup("empty", "Empty"));
            Equal(false, document.SetPrimaryGroup("empty"));
            Equal(true, document.Undo());
            Equal(true, document.SetPrimaryGroup("outer"));
            Equal("outer", document.PrimaryGroupId);
            Equal(true, document.IsPartInGroup("part-a", "outer"));
            Equal(false, document.IsPartInGroup("part-b", "outer"));
            Equal(true, document.Undo());
            Equal(null, document.PrimaryGroupId);
            Equal(true, document.Redo());
            document.SelectOnly("outer");
            Equal(true, document.UngroupSelection());
            Equal(null, document.PrimaryGroupId);
            Equal(true, document.Undo());
            Equal("outer", document.PrimaryGroupId);
            Throws<ArgumentException>(() => new BlueprintEditorDocument(
                null, "Invalid", "Other", document.Parts, document.Groups, null, "missing"));
        }

        private static void BlueprintGroupPivotsAreScopedRemappedAndUndoable()
        {
            var document = new BlueprintEditorDocument(null, "Pivot groups", "Other",
                new[]
                {
                    new BlueprintEditorPart("part-a", "wall", "A", default,
                        IdentityRotation(), "inner"),
                    EditorPart("part-b", 2.0, 0.0, 0.0)
                },
                new[]
                {
                    new BlueprintEditorGroup("outer", "Outer"),
                    new BlueprintEditorGroup("inner", "Inner", parentGroupId: "outer")
                });
            Equal(false, document.SetGroupPivot("missing", "part-a"));
            Equal(false, document.SetGroupPivot("inner", "part-b"));
            Equal(true, document.SetGroupPivot("inner", "part-a"));
            Equal(true, document.SetGroupPivot("outer", "part-a"));
            Equal("part-a", document.Groups[0].PivotPartId);
            Equal("part-a", document.Groups[1].PivotPartId);
            Equal(true, document.SetPrimaryPart("part-b"));

            document.SelectOnly("outer");
            Equal(true, document.DuplicateSelection(new Point3(10, 0, 0)));
            BlueprintEditorGroup outerCopy = document.Groups[2];
            BlueprintEditorGroup innerCopy = document.Groups[3];
            Equal(innerCopy.PivotPartId, outerCopy.PivotPartId);
            Equal(document.Parts[2].StableId, innerCopy.PivotPartId);
            Equal(false, innerCopy.PivotPartId == "part-a");

            Equal(true, document.Undo());
            document.SelectOnly("part-a");
            Equal(true, document.SetSelectionGroup(null));
            Equal(null, document.Groups[0].PivotPartId);
            Equal(null, document.Groups[1].PivotPartId);
            Equal("part-b", document.PrimaryPartId);
            Equal(true, document.Undo());
            Equal("part-a", document.Groups[0].PivotPartId);
            Equal("part-a", document.Groups[1].PivotPartId);
        }

        private static BlueprintEditorDocument GroupedEditorDocument()
        {
            BlueprintEditorDocument document = EditorDocument(
                EditorPart("part-a", 1.0, 0.0, 0.0),
                EditorPart("part-b", 0.0, 1.0, 0.0));
            document.SelectOnly("part-a");
            document.ToggleSelection("part-b");
            Equal(true, document.CreateGroup("group-a", "Group A"));
            return document;
        }

        private static BlueprintEditorDocument EditorDocument(
            params BlueprintEditorPart[] parts) =>
            new BlueprintEditorDocument(null, "Blueprint", "Other", parts);

        private static BlueprintEditorPart EditorPart(
            string stableId,
            double x,
            double y,
            double z) =>
            new BlueprintEditorPart(
                stableId,
                "prefab-" + stableId,
                "Part " + stableId,
                new Point3(x, y, z),
                IdentityRotation());

        private static Rotation3 IdentityRotation() =>
            new Rotation3(0.0, 0.0, 0.0, 1.0);


        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception exception)
            {
                ++failures;
                Console.Error.WriteLine($"FAIL: {name}: {exception.Message}");
            }
        }

        private static void Near(double expected, double actual, double tolerance = 1e-9)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException($"Expected {expected}, got {actual}.");
            }
        }

        private static void Near(Point2 expected, Point2 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Z, actual.Z);
        }

        private static void Near(
            Point3 expected,
            Point3 actual,
            double tolerance = 1e-5)
        {
            Near(expected.X, actual.X, tolerance);
            Near(expected.Y, actual.Y, tolerance);
            Near(expected.Z, actual.Z, tolerance);
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"Expected {expected}, got {actual}.");
            }
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(T).Name}.");
        }
    }
}
