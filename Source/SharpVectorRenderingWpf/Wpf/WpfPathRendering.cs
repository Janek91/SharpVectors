﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

using SharpVectors.Dom.Svg;
using SharpVectors.Runtime;

namespace SharpVectors.Renderers.Wpf
{
    public sealed class WpfPathRendering : WpfRendering
    {
        #region Private Fields

        private DrawingGroup _drawGroup;

        #endregion

        #region Constructors and Destructor

        public WpfPathRendering(SvgElement element)
            : base(element)
        {
        }

        #endregion

        #region Public Methods

        public override void BeforeRender(WpfDrawingRenderer renderer)
        {
            base.BeforeRender(renderer);

            if (renderer == null)
            {
                return;
            }

            WpfDrawingContext context = renderer.Context;

            //SetQuality(context);
            //SetTransform(context);
            //SetMask(context);

            _drawGroup = new DrawingGroup();

            SvgStyleableElement styleElm = (SvgStyleableElement)_svgElement;

            Transform pathTransform = this.Transform;
            if (pathTransform != null && !pathTransform.Value.IsIdentity)
            {
                _drawGroup.Transform = pathTransform;
            }
            else
            {
                pathTransform = null; // render any identity transform useless...
            }
            Geometry pathClip = this.ClipGeometry;
            if (pathClip != null && !pathClip.IsEmpty())
            {
                _drawGroup.ClipGeometry = pathClip;
            }
            else
            {
                pathClip = null; // render any empty geometry useless...
            }
            Brush pathMask = this.Masking;
            if (pathMask != null)
            {
                _drawGroup.OpacityMask = pathMask;
            }

            if (pathTransform != null || pathClip != null || pathMask != null)
            {
                DrawingGroup curGroup = _context.Peek();
                Debug.Assert(curGroup != null);
                if (curGroup != null)
                {
                    curGroup.Children.Add(_drawGroup);
                    context.Push(_drawGroup);
                }
            }
            else
            {
                _drawGroup = null;
            }

            if (_drawGroup != null)
            {
                string sVisibility = styleElm.GetPropertyValue("visibility");
                string sDisplay = styleElm.GetPropertyValue("display");
                if (string.Equals(sVisibility, "hidden") || string.Equals(sDisplay, "none"))
                {
                    _drawGroup.Opacity = 0;
                }

                string elementClass = this.GetElementClass();
                if (!string.IsNullOrWhiteSpace(elementClass) && context.IncludeRuntime)
                {
                    SvgObject.SetClass(_drawGroup, elementClass);
                }

                string elementId = this.GetElementName();
                if (!string.IsNullOrWhiteSpace(elementId) && !context.IsRegisteredId(elementId))
                {
                    SvgObject.SetName(_drawGroup, elementId);

                    context.RegisterId(elementId);

                    if (context.IncludeRuntime)
                    {
                        SvgObject.SetId(_drawGroup, elementId);
                    }
                }
            }
        }

        public override void Render(WpfDrawingRenderer renderer)
        {
            base.Render(renderer);

            if (_drawGroup != null)
            {
                this.RenderGroup(renderer);
            }
            else
            {
                this.RenderPath(renderer);
            }
        }

        private void RenderGroup(WpfDrawingRenderer renderer)
        {
            WpfDrawingContext context = renderer.Context;

            SvgRenderingHint hint = _svgElement.RenderingHint;
            if (hint != SvgRenderingHint.Shape || hint == SvgRenderingHint.Clipping)
            {
                return;
            }
            var parentNode = _svgElement.ParentNode;
            // We do not directly render the contents of the clip-path, unless specifically requested...
            if (string.Equals(parentNode.LocalName, "clipPath") &&
                !context.RenderingClipRegion)
            {
                return;
            }

            SvgStyleableElement styleElm = (SvgStyleableElement)_svgElement;

            string sVisibility = styleElm.GetPropertyValue("visibility");
            string sDisplay    = styleElm.GetPropertyValue("display");
            if (string.Equals(sVisibility, "hidden") || string.Equals(sDisplay, "none"))
            {
                return;
            }

            DrawingGroup drawGroup = context.Peek();
            Debug.Assert(drawGroup != null);

            Geometry geometry = CreateGeometry(_svgElement, context.OptimizePath);

            if (geometry != null && !geometry.IsEmpty())
            {
                context.UpdateBounds(geometry.Bounds);

//                SetClip(context);

                WpfSvgPaint fillPaint = new WpfSvgPaint(context, styleElm, "fill");

                string fileValue = styleElm.GetAttribute("fill");

                Brush brush = fillPaint.GetBrush(geometry);
                bool isFillTransmable = fillPaint.IsFillTransformable;

                WpfSvgPaint strokePaint = new WpfSvgPaint(context, styleElm, "stroke");
                Pen pen = strokePaint.GetPen(geometry);

                if (_paintContext != null)
                {
                    _paintContext.Fill   = fillPaint;
                    _paintContext.Stroke = strokePaint;
                    _paintContext.Tag    = geometry;
                }

                if (brush != null || pen != null)
                {
                    Transform transform = this.Transform;

                    GeometryDrawing drawing = new GeometryDrawing(brush, pen, geometry);

                    Brush maskBrush = this.Masking;
                    Geometry clipGeom = this.ClipGeometry;
                    if (clipGeom != null || maskBrush != null)
                    {
                        //Geometry clipped = Geometry.Combine(geometry, clipGeom,
                        //    GeometryCombineMode.Exclude, null);

                        //if (clipped != null && !clipped.IsEmpty())
                        //{
                        //    geometry = clipped;
                        //}
                        DrawingGroup clipMaskGroup = new DrawingGroup();

                        Rect geometryBounds = geometry.Bounds;

                        if (clipGeom != null)
                        {   
                            clipMaskGroup.ClipGeometry = clipGeom;

                            SvgUnitType clipUnits = this.ClipUnits;
                            if (clipUnits == SvgUnitType.ObjectBoundingBox)
                            {
                                Rect drawingBounds = geometryBounds;

                                if (transform != null)
                                {
                                    drawingBounds = transform.TransformBounds(drawingBounds);
                                }

                                TransformGroup transformGroup = new TransformGroup();

                                // Scale the clip region (at (0, 0)) and translate to the top-left corner of the target.
                                transformGroup.Children.Add(new ScaleTransform(drawingBounds.Width, drawingBounds.Height)); 
                                transformGroup.Children.Add(new TranslateTransform(drawingBounds.X, drawingBounds.Y));

                                clipGeom.Transform = transformGroup;
                            }
                            else
                            {   
                                if (transform != null)
                                {    
                                    clipGeom.Transform = transform;
                                }
                            }
                        }
                        if (maskBrush != null)
                        {
                            DrawingBrush drawingBrush = (DrawingBrush)maskBrush;

                            SvgUnitType maskUnits = this.MaskUnits;
                            SvgUnitType maskContentUnits = this.MaskContentUnits;
                            if (maskUnits == SvgUnitType.ObjectBoundingBox)
                            {
                                Rect drawingBounds = geometryBounds;

                                if (transform != null)
                                {
                                    drawingBounds = transform.TransformBounds(drawingBounds);
                                }
                                DrawingGroup maskGroup = drawingBrush.Drawing as DrawingGroup;
                                if (maskGroup != null)
                                {
                                    DrawingCollection maskDrawings = maskGroup.Children;
                                    for (int i = 0; i < maskDrawings.Count; i++)
                                    {
                                        Drawing maskDrawing = maskDrawings[i];
                                        GeometryDrawing maskGeomDraw = maskDrawing as GeometryDrawing;
                                        if (maskGeomDraw != null)
                                        {
                                            if (maskGeomDraw.Brush != null)
                                            {
                                                ConvertColors(maskGeomDraw.Brush);
                                            }
                                            if (maskGeomDraw.Pen != null)
                                            {
                                                ConvertColors(maskGeomDraw.Pen.Brush);
                                            }
                                        }
                                    }
                                }

                                if (maskContentUnits == SvgUnitType.ObjectBoundingBox)
                                {
                                    TransformGroup transformGroup = new TransformGroup();

                                    // Scale the clip region (at (0, 0)) and translate to the top-left corner of the target.
                                    var scaleTransform = new ScaleTransform(drawingBounds.Width, drawingBounds.Height);
                                    transformGroup.Children.Add(scaleTransform);
                                    var translateTransform = new TranslateTransform(drawingBounds.X, drawingBounds.Y);
                                    transformGroup.Children.Add(translateTransform);

                                    Matrix scaleMatrix = new Matrix();
                                    Matrix translateMatrix = new Matrix();

                                    scaleMatrix.Scale(drawingBounds.Width, drawingBounds.Height);
                                    translateMatrix.Translate(drawingBounds.X, drawingBounds.Y);

                                    Matrix matrix = Matrix.Multiply(scaleMatrix, translateMatrix);
                                    //maskBrush.Transform = transformGroup; 
                                    maskBrush.Transform = new MatrixTransform(matrix); 
                                }
                                else
                                {
                                    drawingBrush.Viewbox = drawingBounds;
                                    drawingBrush.ViewboxUnits = BrushMappingMode.Absolute;

                                    drawingBrush.Stretch = Stretch.Uniform;

                                    drawingBrush.Viewport = drawingBounds;
                                    drawingBrush.ViewportUnits = BrushMappingMode.Absolute;
                                }
                            }
                            else
                            {
                                if (transform != null)
                                {
                                    maskBrush.Transform = transform;
                                }
                            }

                            clipMaskGroup.OpacityMask = maskBrush;
                        }

                        clipMaskGroup.Children.Add(drawing);
                        drawGroup.Children.Add(clipMaskGroup);
                    }
                    else
                    {
                        drawGroup.Children.Add(drawing);
                    }  
                }
            }

            // If this is not the child of a "marker", then try rendering a marker...
            if (!string.Equals(parentNode.LocalName, "marker"))
            {
                RenderMarkers(renderer, styleElm, context);
            }
        }

        private void RenderPath(WpfDrawingRenderer renderer)
        {
            WpfDrawingContext context = renderer.Context;

            SvgRenderingHint hint = _svgElement.RenderingHint;
            if (hint != SvgRenderingHint.Shape || hint == SvgRenderingHint.Clipping)
            {
                return;
            }
            var parentNode = _svgElement.ParentNode;
            // We do not directly render the contents of the clip-path, unless specifically requested...
            if (string.Equals(parentNode.LocalName, "clipPath") &&
                !context.RenderingClipRegion)
            {
                return;
            }

            SvgStyleableElement styleElm = (SvgStyleableElement)_svgElement;

            string sVisibility = styleElm.GetPropertyValue("visibility");
            string sDisplay    = styleElm.GetPropertyValue("display");
            if (string.Equals(sVisibility, "hidden") || string.Equals(sDisplay, "none"))
            {
                return;
            }

            DrawingGroup drawGroup = context.Peek();
            Debug.Assert(drawGroup != null);

            Geometry geometry = CreateGeometry(_svgElement, context.OptimizePath);

            string elementId = this.GetElementName();
            string elementClass = this.GetElementClass();

            if (geometry != null && !geometry.IsEmpty())
            {
                context.UpdateBounds(geometry.Bounds);

//                SetClip(context);

                WpfSvgPaint fillPaint = new WpfSvgPaint(context, styleElm, "fill");

                string fileValue = styleElm.GetAttribute("fill");

                Brush brush = fillPaint.GetBrush(geometry);
                bool isFillTransmable = fillPaint.IsFillTransformable;

                WpfSvgPaint strokePaint = new WpfSvgPaint(context, styleElm, "stroke");
                Pen pen = strokePaint.GetPen(geometry);

                if (_paintContext != null)
                {
                    _paintContext.Fill   = fillPaint;
                    _paintContext.Stroke = strokePaint;
                    _paintContext.Tag    = geometry;
                }

                if (brush != null || pen != null)
                {
                    Transform transform = this.Transform;
                    if (transform != null && !transform.Value.IsIdentity)
                    {
                        geometry.Transform = transform;
                        if (brush != null && isFillTransmable)
                        {
                            Transform brushTransform = brush.Transform;
                            if (brushTransform == null || brushTransform == Transform.Identity)
                            {
                                brush.Transform = transform;
                            }
                            else
                            {
                                TransformGroup groupTransform = new TransformGroup();
                                groupTransform.Children.Add(brushTransform);
                                groupTransform.Children.Add(transform);
                                brush.Transform = groupTransform;
                            }
                        }
                        if (pen != null && pen.Brush != null)
                        {
                            Transform brushTransform = pen.Brush.Transform;
                            if (brushTransform == null || brushTransform == Transform.Identity)
                            {
                                pen.Brush.Transform = transform;
                            }
                            else
                            {
                                TransformGroup groupTransform = new TransformGroup();
                                groupTransform.Children.Add(brushTransform);
                                groupTransform.Children.Add(transform);
                                pen.Brush.Transform = groupTransform;
                            }
                        }
                    }
                    else
                    {
                        transform = null; // render any identity transform useless...
                    }

                    GeometryDrawing drawing = new GeometryDrawing(brush, pen, geometry);

                    if (!string.IsNullOrWhiteSpace(elementId) && !context.IsRegisteredId(elementId))
                    {
                        SvgObject.SetName(drawing, elementId);

                        context.RegisterId(elementId);

                        if (context.IncludeRuntime)
                        {
                            SvgObject.SetId(drawing, elementId);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(elementClass) && context.IncludeRuntime)
                    {
                        SvgObject.SetClass(drawing, elementClass);
                    }

                    Brush maskBrush = this.Masking;
                    Geometry clipGeom = this.ClipGeometry;
                    if (clipGeom != null || maskBrush != null)
                    {
                        //Geometry clipped = Geometry.Combine(geometry, clipGeom,
                        //    GeometryCombineMode.Exclude, null);

                        //if (clipped != null && !clipped.IsEmpty())
                        //{
                        //    geometry = clipped;
                        //}
                        DrawingGroup clipMaskGroup = new DrawingGroup();

                        Rect geometryBounds = geometry.Bounds;

                        if (clipGeom != null)
                        {   
                            clipMaskGroup.ClipGeometry = clipGeom;

                            SvgUnitType clipUnits = this.ClipUnits;
                            if (clipUnits == SvgUnitType.ObjectBoundingBox)
                            {
                                Rect drawingBounds = geometryBounds;

                                if (transform != null)
                                {
                                    drawingBounds = transform.TransformBounds(drawingBounds);
                                }

                                TransformGroup transformGroup = new TransformGroup();

                                // Scale the clip region (at (0, 0)) and translate to the top-left corner of the target.
                                transformGroup.Children.Add(new ScaleTransform(drawingBounds.Width, drawingBounds.Height)); 
                                transformGroup.Children.Add(new TranslateTransform(drawingBounds.X, drawingBounds.Y));

                                clipGeom.Transform = transformGroup;
                            }
                            else
                            {   
                                if (transform != null)
                                {    
                                    clipGeom.Transform = transform;
                                }
                            }
                        }
                        if (maskBrush != null)
                        {
                            DrawingBrush drawingBrush = (DrawingBrush)maskBrush;

                            SvgUnitType maskUnits = this.MaskUnits;
                            SvgUnitType maskContentUnits = this.MaskContentUnits;
                            if (maskUnits == SvgUnitType.ObjectBoundingBox)
                            {
                                Rect drawingBounds = geometryBounds;

                                if (transform != null)
                                {
                                    drawingBounds = transform.TransformBounds(drawingBounds);
                                }
                                DrawingGroup maskGroup = drawingBrush.Drawing as DrawingGroup;
                                if (maskGroup != null)
                                {
                                    DrawingCollection maskDrawings = maskGroup.Children;
                                    for (int i = 0; i < maskDrawings.Count; i++)
                                    {
                                        Drawing maskDrawing = maskDrawings[i];
                                        GeometryDrawing maskGeomDraw = maskDrawing as GeometryDrawing;
                                        if (maskGeomDraw != null)
                                        {
                                            if (maskGeomDraw.Brush != null)
                                            {
                                                ConvertColors(maskGeomDraw.Brush);
                                            }
                                            if (maskGeomDraw.Pen != null)
                                            {
                                                ConvertColors(maskGeomDraw.Pen.Brush);
                                            }
                                        }
                                    }
                                }

                                if (maskContentUnits == SvgUnitType.ObjectBoundingBox)
                                {
                                    TransformGroup transformGroup = new TransformGroup();

                                    // Scale the clip region (at (0, 0)) and translate to the top-left corner of the target.
                                    var scaleTransform = new ScaleTransform(drawingBounds.Width, drawingBounds.Height);
                                    transformGroup.Children.Add(scaleTransform);
                                    var translateTransform = new TranslateTransform(drawingBounds.X, drawingBounds.Y);
                                    transformGroup.Children.Add(translateTransform);

                                    Matrix scaleMatrix = new Matrix();
                                    Matrix translateMatrix = new Matrix();

                                    scaleMatrix.Scale(drawingBounds.Width, drawingBounds.Height);
                                    translateMatrix.Translate(drawingBounds.X, drawingBounds.Y);

                                    Matrix matrix = Matrix.Multiply(scaleMatrix, translateMatrix);
                                    //maskBrush.Transform = transformGroup; 
                                    maskBrush.Transform = new MatrixTransform(matrix); 
                                }
                                else
                                {
                                    drawingBrush.Viewbox = drawingBounds;
                                    drawingBrush.ViewboxUnits = BrushMappingMode.Absolute;

                                    drawingBrush.Stretch = Stretch.Uniform;

                                    drawingBrush.Viewport = drawingBounds;
                                    drawingBrush.ViewportUnits = BrushMappingMode.Absolute;
                                }
                            }
                            else
                            {
                                if (transform != null)
                                {
                                    maskBrush.Transform = transform;
                                }
                            }

                            clipMaskGroup.OpacityMask = maskBrush;
                        }

                        clipMaskGroup.Children.Add(drawing);
                        drawGroup.Children.Add(clipMaskGroup);
                    }
                    else
                    {
                        drawGroup.Children.Add(drawing);
                    }  
                }
            }

            // If this is not the child of a "marker", then try rendering a marker...
            if (!string.Equals(parentNode.LocalName, "marker"))
            {
                RenderMarkers(renderer, styleElm, context);
            }
        }

        public override void AfterRender(WpfDrawingRenderer renderer)
        {
            base.AfterRender(renderer);

            WpfDrawingContext context = renderer.Context;
            if (_drawGroup != null)
            {
                context.Pop();
            }
        }

        //==========================================================================
        private static float AlphaComposition(Color color)
        {
            float max = Math.Max(Math.Max(color.ScR, color.ScG), color.ScB);
            float min = Math.Min(Math.Min(color.ScR, color.ScG), color.ScB);

            return (min + max) / 2.0f;
        }

        //==========================================================================
        private static float AlphaComposition(Brush brush)
        {
            float alphaValue = 1.0f;

            if (brush != null)
            {  
                if (brush is SolidColorBrush)
                {
                    float nextValue = AlphaComposition((brush as SolidColorBrush).Color);
                    if (nextValue > 0 && nextValue < 1)
                    {
                        alphaValue = nextValue;
                    }
                }
                else if (brush is GradientBrush)
                {
                    foreach (GradientStop gradient_stop in (brush as GradientBrush).GradientStops)
                    {
                        float nextValue = AlphaComposition(gradient_stop.Color);
                        if (nextValue > 0 && nextValue < 1)
                        {
                            alphaValue = nextValue;
                        }
                    }
                }
                //else if (brush is DrawingBrush)
                //{
                //    ConvertColors((brush as DrawingBrush).Drawing);
                //}
                else
                {
                    throw new NotSupportedException();
                }
            }

            return alphaValue;
        }

        //==========================================================================
        private static Color ConvertColor(Color color)
        {
            if (color != Colors.Transparent)
            {
                return color;
            }

            float max = Math.Max(Math.Max(color.ScR, color.ScG), color.ScB);
            float min = Math.Min(Math.Min(color.ScR, color.ScG), color.ScB);

            return Color.FromScRgb((min + max) / 2.0f, color.ScR, color.ScG, color.ScB);
        }

        //==========================================================================
        private static void ConvertColors(Brush brush)
        {
            if (brush != null)
            {
                SolidColorBrush solidBrush = null;
                GradientBrush gradientBrush = null;

                if (DynamicCast.Cast(brush, out solidBrush))
                {  
                    solidBrush.Color = ConvertColor(solidBrush.Color);
                }
                else if (DynamicCast.Cast(brush, out gradientBrush))
                {
                    GradientStopCollection stopColl = gradientBrush.GradientStops;

                    foreach (GradientStop stop in stopColl)
                    {
                        stop.Color = ConvertColor(stop.Color);
                    }
                }
                //else if (brush is DrawingBrush)
                //{
                //    ConvertColors((brush as DrawingBrush).Drawing);
                //}
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        #endregion
    }
}
