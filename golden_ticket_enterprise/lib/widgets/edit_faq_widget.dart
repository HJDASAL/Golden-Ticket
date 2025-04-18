import 'package:flutter/material.dart';
import 'package:golden_ticket_enterprise/entities/faq.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:provider/provider.dart';
import 'package:golden_ticket_enterprise/models/data_manager.dart';

class FAQEditWidget extends StatefulWidget {
  final FAQ faq;
  const FAQEditWidget({Key? key, required this.faq}) : super(key: key);

  @override
  _FAQEditWidgetState createState() => _FAQEditWidgetState();
}

class _FAQEditWidgetState extends State<FAQEditWidget> {
  late TextEditingController titleController;
  late TextEditingController descriptionController;
  late TextEditingController solutionController;
  late bool isArchived;
  String? mainTag;
  String? subTag;

  @override
  void initState() {
    super.initState();
    // Initializing controllers with the current FAQ data
    titleController = TextEditingController(text: widget.faq.title);
    descriptionController = TextEditingController(text: widget.faq.description);
    solutionController = TextEditingController(text: widget.faq.solution);
    isArchived = widget.faq.isArchived;

    // Set initial tags
    mainTag = widget.faq.mainTag?.tagName ?? "Select a main tag";
    subTag = widget.faq.subTag?.subTagName ?? "Select a sub tag";
  }

  @override
  void dispose() {
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final dataManager = Provider.of<DataManager>(context);

    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: Container(
        width: MediaQuery.of(context).size.width > 600 ? 500 : double.infinity,
        padding: EdgeInsets.all(20),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Edit FAQ', style: Theme.of(context).textTheme.headlineMedium),
              SizedBox(height: 10),

              _buildTitleField(),
              SizedBox(height: 10),
              _buildTextField("Description", descriptionController, 300),
              SizedBox(height: 10),
              _buildTextField("Solution", solutionController, 1200),
              SizedBox(height: 10),

              // MainTag Dropdown
              _buildDropdown("Main Tag", dataManager.mainTags.map((tag) => tag.tagName).toList(), (newValue) {
                setState(() {
                  mainTag = newValue;
                  subTag = null;  // Reset subTag when mainTag is changed
                });
              }),

              SizedBox(height: 10),

              // SubTag Dropdown
              _buildDropdown(
                "Sub Tag",
                mainTag != null && mainTag != 'None'
                    ? dataManager.mainTags.firstWhere((m) => m.tagName == mainTag).subTags.map((tag) => tag.subTagName).toList()
                    : ['None'],
                    (newValue) {
                  setState(() {
                    subTag = newValue;
                  });
                },
              ),

              SizedBox(height: 10),

              Row(
                children: [
                  Checkbox(
                    value: isArchived,
                    onChanged: (bool? value) {
                      setState(() {
                        isArchived = value ?? false;
                      });
                    },
                  ),
                  Text('Is Archived'),
                ],
              ),
              SizedBox(height: 20),

              // Save and Cancel buttons
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    onPressed: () {
                      Navigator.pop(context); // Cancel button action
                    },
                    child: Text('Cancel'),
                  ),
                  SizedBox(width: 10),
                  ElevatedButton(
                    onPressed: () {
                      if (_validateFields()) {
                        dataManager.signalRService.updateFAQ(widget.faq.faqID, titleController.text, descriptionController.text, solutionController.text, mainTag, subTag, isArchived);
                        Navigator.pop(context); // Pass updated FAQ back
                      }
                    },
                    child: Text('Save'),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  bool _validateFields() {
    if (titleController.text.isEmpty || descriptionController.text.isEmpty || solutionController.text.isEmpty || mainTag == null || subTag == null) {
      _showErrorDialog("All fields must be filled!");
      return false;
    }
    if (titleController.text.length > 80) {
      _showErrorDialog("Title must be less than 80 characters.");
      return false;
    }
    if (descriptionController.text.length > 300) {
      _showErrorDialog("Description must be less than 300 characters.");
      return false;
    }
    if (solutionController.text.length > 1200) {
      _showErrorDialog("Solution must be less than 1200 characters.");
      return false;
    }
    return true;
  }

  void _showErrorDialog(String message) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Error'),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.pop(context);
            },
            child: Text('OK'),
          ),
        ],
      ),
    );
  }

  Widget _buildTitleField() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text("Title"),
        Text(
          "${titleController.text.length}/80 characters",
          style: TextStyle(fontSize: 12, color: Colors.grey),
        ),
        TextField(
          controller: titleController,
          decoration: InputDecoration(
            border: OutlineInputBorder(),
            hintText: 'Enter FAQ Title',
          ),
          maxLength: 100,
          onChanged: (text) {
            setState(() {});
          },
        ),
      ],
    );
  }

  Widget _buildTextField(String label, TextEditingController controller, int maxLength) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: TextStyle(fontWeight: FontWeight.bold)),
        Text(
          "${controller.text.length}/$maxLength characters",
          style: TextStyle(fontSize: 12, color: Colors.grey),
        ),
        TextField(
          controller: controller,
          decoration: InputDecoration(
            border: OutlineInputBorder(),
            hintText: 'Enter $label',
          ),
          maxLength: maxLength,
          maxLines: null,
          onChanged: (text) {
            setState(() {});
          },
        ),
        SizedBox(height: 10),
        MarkdownBody(data: controller.text), // Show markdown preview for Description and Solution
      ],
    );
  }

  Widget _buildDropdown(String label, List<String> items, ValueChanged<String?> onChanged) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label),
        DropdownButton<String>(
          value: label == "Main Tag" ? mainTag : subTag,
          hint: Text("Select $label"),
          isExpanded: true,
          onChanged: onChanged,
          items: items.map<DropdownMenuItem<String>>((String value) {
            return DropdownMenuItem<String>(value: value, child: Text(value));
          }).toList(),
        ),
      ],
    );
  }
}
