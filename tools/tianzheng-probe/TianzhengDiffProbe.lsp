;;; SpatialViewer.CadCore — Tianzheng structural research probe
;;; Commands: TCHDIFF, TCHSIG, TCHPLAN, TCHRUN
;;;
;;; TCHDIFF compares two controlled Tianzheng custom objects without printing
;;; raw DXF values, handles, entity names, or file paths. For the unresolved
;;; v0.12 gates it can bind output to one canonical single-variable case.
;;;
;;; TCHSIG prints only one object's TCH_* type, structural counts, and ordered
;;; DXF group-code signature. It never prints group values or subclass names.
;;;
;;; TCHRUN performs one atomic canonical experiment: it validates the baseline
;;; and modified objects first, then emits a TCHSIG + case-bound TCHDIFF bundle
;;; from that same A/B pair. No protocol bundle is emitted on structural mismatch.
;;;
;;; This tool is research evidence only. A changed slot is not automatically a
;;; dimension scale, axis number, drawing-index number, or index-pointer field.

(vl-load-com)

(defun tchdiff:_code (entry)
  ;; entget entries are cons/dotted-pair values. Do not require a proper list;
  ;; the only contract needed here is a numeric DXF group code in car.
  (if (and entry (numberp (car entry)))
    (car entry)
    nil
  )
)

(defun tchdiff:_filtered (ename / data out code)
  (setq data (entget ename))
  (foreach entry data
    (setq code (tchdiff:_code entry))
    ;; -1 is the transient entity name and 5 is the unique object handle.
    ;; Both always differ across duplicated A/B objects and are not useful
    ;; semantic candidates, so they are deliberately excluded.
    (if (and code (/= code -1) (/= code 5))
      (setq out (cons entry out))
    )
  )
  (reverse out)
)

(defun tchdiff:_type (data / item)
  (setq item (assoc 0 data))
  (if item (cdr item) nil)
)

(defun tchdiff:_tch-type-p (name)
  (and name (wcmatch (strcase name) "TCH_*"))
)

(defun tchdiff:_subclasses (data / out)
  (foreach entry data
    (if (= (tchdiff:_code entry) 100)
      (setq out (cons (cdr entry) out))
    )
  )
  (reverse out)
)

(defun tchdiff:_codes (data / out)
  (foreach entry data
    (setq out (cons (tchdiff:_code entry) out))
  )
  (reverse out)
)

(defun tchdiff:_occurrence (data stop-index code / i count entry)
  (setq i 0 count 0)
  (foreach entry data
    (if (< i stop-index)
      (if (= (tchdiff:_code entry) code)
        (setq count (1+ count))
      )
    )
    (setq i (1+ i))
  )
  (1+ count)
)

(defun tchdiff:_first-code-mismatch (left right / i limit lc rc found)
  (setq i 0
        limit (min (length left) (length right)))
  (while (and (< i limit) (not found))
    (setq lc (nth i left)
          rc (nth i right))
    (if (/= lc rc)
      (setq found (list i lc rc))
    )
    (setq i (1+ i))
  )
  (if found
    found
    (if (/= (length left) (length right))
      (list limit nil nil)
      nil
    )
  )
)

(defun tchdiff:_case-info (key)
  ;; Returns (protocol-case-id expected-dxf-name). Adhoc intentionally stays
  ;; untagged for non-gate research and preserves the original TCHDIFF mode.
  (cond
    ((equal key "Axis")     (list "AXIS_LABEL_TEXT" "TCH_AXIS_LABEL"))
    ((equal key "Index")    (list "DRAWING_INDEX_TEXT" "TCH_DRAWINGINDEX"))
    ((equal key "Pointer")  (list "INDEX_POINTER_TEXT" "TCH_INDEXPOINTER"))
    ((equal key "DimScale") (list "DIMENSION_PLOT_SCALE" "TCH_DIMENSION2"))
    (T nil)
  )
)

(defun tchdiff:_print-code-signature (codes / first code)
  (princ "\n[TCHSIG] code-signature=")
  (setq first T)
  (foreach code codes
    (if first
      (setq first nil)
      (princ ",")
    )
    (princ (itoa code))
  )
)

(defun tchdiff:_emit-signature-data (data type / codes)
  (setq codes (tchdiff:_codes data))
  (princ (strcat "\n[TCHSIG] Object type=" type))
  (princ (strcat "\n[TCHSIG] Entry count=" (itoa (length data))))
  (princ (strcat "\n[TCHSIG] Subclass marker count=" (itoa (length (tchdiff:_subclasses data)))))
  (tchdiff:_print-code-signature codes)
)

(defun tchdiff:_print-layout-mismatch (left-codes right-codes / mismatch)
  (setq mismatch (tchdiff:_first-code-mismatch left-codes right-codes))
  (princ
    (strcat
      "\n[TCHDIFF] Structural layout differs; value-slot comparison stopped."
      " before-count=" (itoa (length left-codes))
      " after-count=" (itoa (length right-codes))))
  (if mismatch
    (progn
      (princ (strcat "\n[TCHDIFF] First structural mismatch slot=" (itoa (car mismatch))))
      (if (cadr mismatch)
        (princ (strcat " before-code=" (itoa (cadr mismatch))))
      )
      (if (caddr mismatch)
        (princ (strcat " after-code=" (itoa (caddr mismatch))))
      )
    )
  )
  (princ "\n[TCHDIFF] Do not heuristically align shifted/repeated proprietary groups.")
)

(defun tchdiff:_compare-values (left right / i entry-left entry-right code occurrence changed)
  (setq i 0 changed 0)
  (while (< i (length left))
    (setq entry-left (nth i left)
          entry-right (nth i right)
          code (tchdiff:_code entry-left))
    ;; A tiny numeric fuzz avoids reporting representation noise while still
    ;; treating real controlled property changes as different.
    (if (not (equal entry-left entry-right 1e-12))
      (progn
        (setq occurrence (tchdiff:_occurrence left i code)
              changed (1+ changed))
        (princ
          (strcat
            "\n[TCHDIFF] changed slot=" (itoa i)
            " code=" (itoa code)
            " occurrence=" (itoa occurrence)))
      )
    )
    (setq i (1+ i))
  )
  (if (= changed 0)
    (princ "\n[TCHDIFF] No value changes found after identity fields were excluded.")
    (princ (strcat "\n[TCHDIFF] Changed candidate count=" (itoa changed)))
  )
)

(defun c:TCHPLAN ()
  (princ "\nTCHPLAN — v0.12 canonical controlled experiments")
  (princ "\n  Axis     -> AXIS_LABEL_TEXT / TCH_AXIS_LABEL")
  (princ "\n  Index    -> DRAWING_INDEX_TEXT / TCH_DRAWINGINDEX")
  (princ "\n  Pointer  -> INDEX_POINTER_TEXT / TCH_INDEXPOINTER")
  (princ "\n  DimScale -> DIMENSION_PLOT_SCALE / TCH_DIMENSION2")
  (princ "\nFor each gate case, change exactly the named UI property and nothing else.")
  (princ "\nRun at least two independent A/B pairs before CadCore consensus.")
  (princ "\nUse TCHRUN for an atomic signature + diff transcript from one validated pair.")
  (princ)
)

(defun c:TCHSIG (/ pick data type)
  (princ "\nTCHSIG — privacy-safe Tianzheng structural signature")
  (setq pick (car (entsel "\nSelect Tianzheng object: ")))
  (if (not pick)
    (princ "\n[TCHSIG] Cancelled.")
    (progn
      (setq data (tchdiff:_filtered pick)
            type (tchdiff:_type data))
      (if (not (tchdiff:_tch-type-p type))
        (princ "\n[TCHSIG] Refused: selection must be a TCH_* custom object.")
        (tchdiff:_emit-signature-data data type)
      )
    )
  )
  (princ)
)

(defun c:TCHDIFF (/ case-key case-info expected-type pick-a pick-b data-a data-b type-a type-b codes-a codes-b)
  (princ "\nTCHDIFF — privacy-safe Tianzheng controlled A/B structural probe")
  (initget "Axis Index Pointer DimScale Adhoc")
  (setq case-key (getkword "\nExperiment case [Axis/Index/Pointer/DimScale/Adhoc] <Adhoc>: "))
  (if (not case-key) (setq case-key "Adhoc"))
  (setq case-info (tchdiff:_case-info case-key)
        expected-type (if case-info (cadr case-info) nil))

  (setq pick-a (car (entsel "\nSelect BASELINE Tianzheng object: ")))
  (if (not pick-a)
    (princ "\n[TCHDIFF] Cancelled.")
    (progn
      (setq pick-b (car (entsel "\nSelect MODIFIED Tianzheng object: ")))
      (if (not pick-b)
        (princ "\n[TCHDIFF] Cancelled.")
        (progn
          (setq data-a (tchdiff:_filtered pick-a)
                data-b (tchdiff:_filtered pick-b)
                type-a (tchdiff:_type data-a)
                type-b (tchdiff:_type data-b))

          (cond
            ((or (not (tchdiff:_tch-type-p type-a))
                 (not (tchdiff:_tch-type-p type-b)))
             (princ "\n[TCHDIFF] Refused: both selections must be TCH_* custom objects."))

            ((not (equal (strcase type-a) (strcase type-b)))
             (princ "\n[TCHDIFF] Refused: DXF object identities differ."))

            ((and expected-type (not (equal (strcase type-a) expected-type)))
             (princ
               (strcat
                 "\n[TCHDIFF] Refused: experiment case expects object type="
                 expected-type)))

            ((not (equal (tchdiff:_subclasses data-a)
                         (tchdiff:_subclasses data-b)))
             (princ "\n[TCHDIFF] Refused: subclass identity/profile differs."))

            (T
             (if case-info
               (princ (strcat "\n[TCHDIFF] Case=" (car case-info)))
             )
             (princ (strcat "\n[TCHDIFF] Object type=" type-a))
             (setq codes-a (tchdiff:_codes data-a)
                   codes-b (tchdiff:_codes data-b))
             (if (equal codes-a codes-b)
               (tchdiff:_compare-values data-a data-b)
               (tchdiff:_print-layout-mismatch codes-a codes-b)
             )
            )
          )
        )
      )
    )
  )
  (princ)
)

(defun c:TCHRUN (/ case-key case-info expected-type pick-a pick-b data-a data-b type-a type-b codes-a codes-b)
  (princ "\nTCHRUN — atomic v0.12 Tianzheng controlled experiment")
  (initget 1 "Axis Index Pointer DimScale")
  (setq case-key (getkword "\nExperiment case [Axis/Index/Pointer/DimScale]: ")
        case-info (tchdiff:_case-info case-key)
        expected-type (cadr case-info))

  (setq pick-a (car (entsel "\nSelect BASELINE Tianzheng object: ")))
  (if (not pick-a)
    (princ "\n[TCHRUN] Cancelled.")
    (progn
      (setq pick-b (car (entsel "\nSelect MODIFIED Tianzheng object: ")))
      (if (not pick-b)
        (princ "\n[TCHRUN] Cancelled.")
        (progn
          (setq data-a (tchdiff:_filtered pick-a)
                data-b (tchdiff:_filtered pick-b)
                type-a (tchdiff:_type data-a)
                type-b (tchdiff:_type data-b))

          ;; TCHRUN emits no parsable TCHSIG/TCHDIFF bundle until every identity
          ;; and structural check succeeds. This keeps a copied transcript atomic.
          (cond
            ((or (not (tchdiff:_tch-type-p type-a))
                 (not (tchdiff:_tch-type-p type-b)))
             (princ "\n[TCHRUN] Refused: both selections must be TCH_* custom objects."))

            ((not (equal (strcase type-a) (strcase type-b)))
             (princ "\n[TCHRUN] Refused: DXF object identities differ."))

            ((not (equal (strcase type-a) expected-type))
             (princ (strcat "\n[TCHRUN] Refused: experiment case expects object type=" expected-type)))

            ((not (equal (tchdiff:_subclasses data-a)
                         (tchdiff:_subclasses data-b)))
             (princ "\n[TCHRUN] Refused: subclass identity/profile differs."))

            (T
             (setq codes-a (tchdiff:_codes data-a)
                   codes-b (tchdiff:_codes data-b))
             (if (not (equal codes-a codes-b))
               (princ "\n[TCHRUN] Refused: group-code layout differs; no atomic bundle emitted.")
               (progn
                 (princ (strcat "\n[TCHDIFF] Case=" (car case-info)))
                 (princ (strcat "\n[TCHDIFF] Object type=" type-a))
                 (tchdiff:_emit-signature-data data-a type-a)
                 (tchdiff:_compare-values data-a data-b)
                 (princ "\n[TCHRUN] Bundle complete. Copy this command output as one experiment transcript.")
               )
             )
            )
          )
        )
      )
    )
  )
  (princ)
)

(princ "\nSpatialViewer Tianzheng probe loaded. Run TCHPLAN, TCHRUN, TCHDIFF or TCHSIG.")
(princ)
