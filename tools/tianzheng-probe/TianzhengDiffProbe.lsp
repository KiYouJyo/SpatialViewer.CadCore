;;; SpatialViewer.CadCore — Tianzheng A/B structural probe
;;; Command: TCHDIFF
;;;
;;; Purpose:
;;;   Compare two controlled Tianzheng custom objects inside AutoCAD/Tianzheng
;;;   without printing raw DXF values, handles, entity names, or file paths.
;;;
;;; Recommended experiment:
;;;   1. Create two otherwise-equivalent Tianzheng objects.
;;;   2. Change exactly one known property on the second object.
;;;   3. Run TCHDIFF and select baseline, then modified object.
;;;   4. Record only the reported group code / occurrence / slot candidates.
;;;   5. Repeat with an independent pair before assigning semantic meaning.
;;;
;;; This tool is research evidence only. A changed slot is not automatically a
;;; column width, stair height, dimension scale, axis number, or index field.

(vl-load-com)

(defun tchdiff:_code (entry)
  (if (and (listp entry) (numberp (car entry)))
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

(defun c:TCHDIFF (/ pick-a pick-b data-a data-b type-a type-b codes-a codes-b)
  (princ "\nTCHDIFF — privacy-safe Tianzheng controlled A/B structural probe")
  (setq pick-a (car (entsel "\nSelect BASELINE Tianzheng object: ")))
  (if (not pick-a)
    (progn (princ "\n[TCHDIFF] Cancelled.") (princ))
    (progn
      (setq pick-b (car (entsel "\nSelect MODIFIED Tianzheng object: ")))
      (if (not pick-b)
        (progn (princ "\n[TCHDIFF] Cancelled.") (princ))
        (progn
          (setq data-a (tchdiff:_filtered pick-a)
                data-b (tchdiff:_filtered pick-b)
                type-a (tchdiff:_type data-a)
                type-b (tchdiff:_type data-b))

          (cond
            ((or (not (tchdiff:_tch-type-p type-a))
                 (not (tchdiff:_tch-type-p type-b)))
             (princ "\n[TCHDIFF] Refused: both selections must be TCH_* custom objects."))

            ((/= (strcase type-a) (strcase type-b))
             (princ "\n[TCHDIFF] Refused: DXF object identities differ."))

            ((not (equal (tchdiff:_subclasses data-a)
                         (tchdiff:_subclasses data-b)))
             (princ "\n[TCHDIFF] Refused: subclass identity/profile differs."))

            (T
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

(princ "\nSpatialViewer Tianzheng probe loaded. Run TCHDIFF.")
(princ)
