# Rendered conversation PDF regression

GitHub Actions runs `scripts/check-chat-pdf.py` on its Xcode runner. It builds a
small native simulator application containing the **same renderer source used
by the shipping app**, and prints a recorded conversation with a table, code,
source link, graph and ninety paragraphs. Nothing is built or run locally. The synthetic simulator bundle is ad-hoc signed and verified after its unsigned build; it uses no distribution credentials.

A reference harness prints the same model with desktop's PDF writer and Markdown
parser, fetched at commit `88600f8c084953103cb1df59ccff9b0e800fbd2c` with both
source hashes verified. The only adaptations are the Mobile export model namespace
and the bundled-font file source. The reference code remains copyright Thomas
Pickup under desktop's licence; it is downloaded for this authorised parity
check, not added to Mobile's source or app.

`rendered.golden.json` is a shared semantic/raster golden for both actual PDFs:
selectable text through every page, all recorded paragraphs, a minimum page
count, clickable source annotations, image objects and visible red graph pixels.
The PDFs, page rasters and comparison facts are retained as CI evidence. Different
native and desktop typography or page counts are allowed; content omissions,
blank graphs, lost pages and inactive source links fail. This does not claim
pixel-identical typography or installed-device UI acceptance.
